using Azure.AI.OpenAI;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Caching.Distributed;
using OpenAI.Chat;
using SednaRag.Models;
using SednaRag.Services.Clients;
using SharpToken;
using System.Text;
using System.Text.Json;

namespace SednaRag.Services
{
    public class SupportAgentService
    {
        private readonly AzureOpenAIClient _openAIClient;
        private readonly SearchClient _searchClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<SupportAgentService> _logger;
        private readonly IDistributedCache _cache;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly HttpClient _httpClient;

        public SupportAgentService(
            AzureOpenAIClient openAIClient,
            SupportSearchClient searchClientWrapper,
            IConfiguration configuration,
            IDistributedCache cache,
            ILogger<SupportAgentService> logger,
            IHttpContextAccessor httpContextAccessor,
            IHttpClientFactory httpClientFactory)
        {
            _openAIClient = openAIClient;
            _searchClient = searchClientWrapper.Client;
            _configuration = configuration;
            _cache = cache;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            _httpClient = httpClientFactory.CreateClient("LicenseService");
        }

        // Processa una query di supporto in linguaggio naturale
        public async Task<SupportQueryResponse> ProcessQueryAsync(
            string query,
            string clientId,
            string module = null)
        {
            try
            {
                // Ottieni l'API key e le altre informazioni dal contesto della richiesta
                string apiKey = _httpContextAccessor.HttpContext.Items["ApiKey"] as string;
                int richiesteDisponibili = _httpContextAccessor.HttpContext.Items["RichiesteDisponibili"] != null
                    ? (int)_httpContextAccessor.HttpContext.Items["RichiesteDisponibili"]
                    : 0;

                if (richiesteDisponibili <= 0)
                {
                    throw new InvalidOperationException("Limite di richieste AI raggiunto. Acquista ulteriori crediti.");
                }

                // Verifica se la risposta è in cache
                string cacheKey = $"support_query_{clientId}_{module ?? "all"}_{ComputeHash(query)}";
                var cachedResponse = await _cache.GetStringAsync(cacheKey);
                if (!string.IsNullOrEmpty(cachedResponse))
                {
                    _logger.LogInformation("Risposta di supporto trovata in cache per query: {Query}", query);
                    return JsonSerializer.Deserialize<SupportQueryResponse>(cachedResponse);
                }

                // Calcola token di embedding (approssimazione)
                int embeddingTokens = EstimateTokenCount(query);

                // 1. Recupera documenti di supporto pertinenti
                var relevantDocuments = await RetrieveRelevantSupportDocumentsAsync(query, clientId, module);

                // 2. Genera la risposta basata sui documenti recuperati
                var supportResponse = await GenerateSupportResponseAsync(query, relevantDocuments);

                // Calcola token effettivamente utilizzati in questa query
                int tokensUsedInQuery = supportResponse.InputTokens +
                                       supportResponse.OutputTokens +
                                       embeddingTokens;

                // Aggiorna il conteggio dei token nel servizio licenze
                var tokenUpdateRequest = new TokenUsageRequest
                {
                    ApiKey = apiKey,
                    TokensUsed = tokensUsedInQuery,
                    Module = module,
                    PromptType = "Support Query",
                    QueryText = query
                };

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("ApiKey", $"{apiKey}");

                var updateResponse = await _httpClient.PostAsJsonAsync(
                    $"updateTokenAI/{apiKey}",
                    tokenUpdateRequest);

                if (!updateResponse.IsSuccessStatusCode)
                {
                    _logger.LogError("Errore durante l'aggiornamento del conteggio token: {StatusCode}", updateResponse.StatusCode);
                    throw new InvalidOperationException("Errore nell'aggiornamento dei token consumati");
                }

                // Leggi i token rimanenti dalla risposta
                var tokenResponse = await updateResponse.Content.ReadFromJsonAsync<TokenUsageResponse>();

                // Costruisci risposta
                var response = new SupportQueryResponse
                {
                    Success = true,
                    Response = supportResponse.Response,
                    RelevantDocuments = relevantDocuments.Select(d => new SupportDocumentReference
                    {
                        Id = d.Id,
                        Title = d.Title,
                        Snippet = TruncateContent(d.Content, 200),
                        DocumentType = d.DocumentType
                    }).ToList(),
                    TokensUsed = new TokenUsage
                    {
                        InputTokens = supportResponse.InputTokens,
                        OutputTokens = supportResponse.OutputTokens,
                        TotalTokens = supportResponse.InputTokens + supportResponse.OutputTokens,
                        EmbeddingTokens = embeddingTokens,
                        TotalInQuery = tokensUsedInQuery
                    },
                    TokensRemaining = tokenResponse?.TokensRemaining ?? 0
                };

                // Salva in cache per future richieste
                var cacheOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24),
                    SlidingExpiration = TimeSpan.FromMinutes(60)
                };

                await _cache.SetStringAsync(
                    cacheKey,
                    JsonSerializer.Serialize(response),
                    cacheOptions);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nel processing della query di supporto");
                throw;
            }
        }

        // Metodo helper per stimare i token
        private int EstimateTokenCount(string text)
        {
            var encoding = GptEncoding.GetEncodingForModel(_configuration["Azure:OpenAI:DeploymentName"]);
            int tokenCount = encoding.Encode(text).Count;
            return tokenCount;
        }

        private async Task<List<SupportDocument>> RetrieveRelevantSupportDocumentsAsync(
            string query,
            string clientId,
            string module = null)
        {
            try
            {
                // Costruisci filtro
                string filter = $"clientId eq '{clientId}' or clientId eq 'common'";
                if (!string.IsNullOrEmpty(module))
                {
                    filter += $" and (module eq '{module}' or module eq 'all')";
                }

                // Prima genera embedding della query per ricerca vettoriale
                var embeddingModelName = _configuration["Azure:OpenAI:EmbeddingModel"] ?? "text-embedding-ada-002";
                var embeddingClient = _openAIClient.GetEmbeddingClient(embeddingModelName);
                var embeddingResponse = await embeddingClient.GenerateEmbeddingAsync(query);
                var queryVector = embeddingResponse.Value.ToFloats();

                // Configura ricerca vettoriale
                var searchOptions = new SearchOptions
                {
                    Filter = filter,
                    Size = 5,
                    Select = { "id", "clientId", "title", "content", "documentType", "module", "tags" }
                };

                // Aggiungi configurazione vettoriale
                searchOptions.VectorSearch = new VectorSearchOptions
                {
                    Queries = { new VectorizedQuery(queryVector)
                    {
                        KNearestNeighborsCount = 5,
                        Fields = { "contentVector" }
                    }}
                };

                // Esegui ricerca
                var searchResults = await _searchClient.SearchAsync<SupportDocument>("", searchOptions);

                // Estrai documenti
                var documents = new List<SupportDocument>();
                foreach (var result in searchResults.Value.GetResults())
                {
                    documents.Add(result.Document);
                }

                _logger.LogInformation("Recuperati {Count} documenti di supporto rilevanti", documents.Count);

                return documents;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nel recupero documenti di supporto rilevanti");
                throw;
            }
        }

        private async Task<(string Response, int InputTokens, int OutputTokens)> GenerateSupportResponseAsync(
            string query,
            List<SupportDocument> relevantDocuments)
        {
            try
            {
                // Costruisci prompt con contesto rilevante
                var contextText = new StringBuilder();
                foreach (var doc in relevantDocuments)
                {
                    contextText.AppendLine($"--- {doc.Title} ---");
                    contextText.AppendLine(doc.Content);
                    contextText.AppendLine();
                }

                // Prompt system per l'AI
                string systemPrompt = $@"
Sei un assistente esperto del software ERP per il settore ortofrutticolo. Il tuo compito è fornire supporto tecnico e aiuto sull'utilizzo del software in base alla richiesta dell'utente.
Utilizza SOLO le informazioni contenute nella seguente documentazione di supporto:

{contextText}

Fornisci una risposta utile e pertinente alla domanda dell'utente. Sii conciso ma esaustivo. 
Se non hai informazioni sufficienti per rispondere alla domanda, dichiara onestamente che non hai abbastanza informazioni e suggerisci di contattare il supporto tecnico.
Non inventare informazioni che non sono presenti nella documentazione fornita.";

                // Configura il client di chat
                string deploymentName = _configuration["Azure:OpenAI:DeploymentName"];
                var chatClient = _openAIClient.GetChatClient(deploymentName);

                // Prepara la lista di messaggi per la chat
                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage(systemPrompt),
                    new UserChatMessage(query)
                };

                // Configura le opzioni con temperatura bassa per risposte più deterministiche
                var chatCompletionOptions = new ChatCompletionOptions
                {
                    Temperature = 0.3f,
                    MaxOutputTokenCount = 1000
                };

                // Ottieni la risposta dall'API
                var response = await chatClient.CompleteChatAsync(messages, chatCompletionOptions);
                string responseContent = response.Value.Content[0].Text;

                // Calcola l'utilizzo di token
                int inputTokens = response.Value.Usage.InputTokenCount;
                int outputTokens = response.Value.Usage.OutputTokenCount;

                return (responseContent, inputTokens, outputTokens);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nella generazione della risposta di supporto");
                throw;
            }
        }

        private string TruncateContent(string content, int maxLength)
        {
            if (string.IsNullOrEmpty(content))
                return string.Empty;

            if (content.Length <= maxLength)
                return content;

            return content.Substring(0, maxLength) + "...";
        }

        // Calcola hash della query per chiave cache
        private string ComputeHash(string input)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            var bytes = System.Text.Encoding.UTF8.GetBytes(input);
            var hash = sha.ComputeHash(bytes);
            return Convert.ToBase64String(hash).Replace("/", "_").Replace("+", "-").Replace("=", "");
        }
    }
}