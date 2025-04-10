using Azure.AI.OpenAI;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Identity.Client;
using OpenAI.Chat;
using SednaRag.Models;
using SharpToken;
using System.ClientModel;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace SednaRag.Services
{
    public class RagService
    {

 

        private readonly AzureOpenAIClient _openAIClient;
        private readonly SearchClient _searchClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<RagService> _logger;
        private readonly IDistributedCache _cache;
        private  HttpClient _httpClient;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private string LicenseServiceBaseUrl => _configuration["LicenseService:BaseUrl"];

        public RagService(
            AzureOpenAIClient openAIClient,
            SearchClient searchClient,
            IConfiguration configuration,
            IDistributedCache cache,
            ILogger<RagService> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _openAIClient = openAIClient;
            _searchClient = searchClient;
            _configuration = configuration;
            _cache = cache;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        // Processa una query in linguaggio naturale
        public async Task<RagQueryResponse> ProcessQueryAsync(
            string query,
            string clientId,
            string module = null)
        {
            try
            {
                // Ottieni l'API key dagli header o dal contesto della richiesta
                // Ottieni l'API key e tutte le altre informazioni dal contesto della richiesta
                string apiKey = _httpContextAccessor.HttpContext.Items["ApiKey"] as string;
                int richiesteDisponibili = _httpContextAccessor.HttpContext.Items["RichiesteDisponibili"] != null
                    ? (int)_httpContextAccessor.HttpContext.Items["RichiesteDisponibili"]
                    : 0;
                int lastWeekUsage = _httpContextAccessor.HttpContext.Items["LastWeekUsage"] != null
                    ? (int)_httpContextAccessor.HttpContext.Items["LastWeekUsage"]
                    : 0;
                string ragioneSociale = _httpContextAccessor.HttpContext.Items["RagioneSociale"] as string;

                if (richiesteDisponibili <= 0)
                {
                    throw new InvalidOperationException("Limite di richieste AI raggiunto. Acquista ulteriori crediti.");
                }



                // Verifica se la risposta è in cache
                string cacheKey = $"query_{clientId}_{module ?? "all"}_{ComputeHash(query)}";

                var cachedResponse = await _cache.GetStringAsync(cacheKey);
                if (!string.IsNullOrEmpty(cachedResponse))
                {
                    _logger.LogInformation("Risposta trovata in cache per query: {Query}", query);
                    return JsonSerializer.Deserialize<RagQueryResponse>(cachedResponse);
                }
               

                // Calcola token di embedding (approssimazione)
                int embeddingTokens = EstimateTokenCount(query);
                // 1. Recupera schema e logiche pertinenti
                var relevantDocuments = await RetrieveRelevantDocumentsAsync(query, clientId, module);

                // 2. Genera la query SQL basata sul contesto
                var generatedQuery = await GenerateSqlQueryAsync(query, relevantDocuments);

                // Calcola token effettivamente utilizzati in questa query
                int tokensUsedInQuery = generatedQuery.InputTokens +
                                      generatedQuery.OutputTokens +
                                      embeddingTokens;

                // Nuovi token residui dopo questa query
                //int newTokensRemaining = tokensRemaining - tokensUsedInQuery;

                // Aggiorna il conteggio dei token nel servizio licenze
                var tokenUpdateRequest = new TokenUsageRequest
                {
                    ApiKey = apiKey,
                    TokensUsed = tokensUsedInQuery,
                    Module = module,
                    PromptType = "Query User",
                    QueryText = query
                };
                _httpClient = new HttpClient();
                _httpClient.DefaultRequestHeaders.Add("ApiKey", $"{apiKey}");

                var updateResponse = await _httpClient.PostAsJsonAsync(
                    $"{LicenseServiceBaseUrl}/updateTokenAI/{apiKey}",
                    tokenUpdateRequest);

                if (!updateResponse.IsSuccessStatusCode)
                {
                    _logger.LogError("Errore durante l'aggiornamento del conteggio token: {StatusCode}", updateResponse.StatusCode);
                    throw new InvalidOperationException("Errore nell'aggiornamento dei token consumati");
                }

                // Leggi i token rimanenti dalla risposta
                var tokenResponse = await updateResponse.Content.ReadFromJsonAsync<TokenUsageResponse>();


                // Costruisci risposta
                var response = new RagQueryResponse
                {
                    Success = true,
                    QueryGenerated = generatedQuery.SqlQuery,
                    Explanation = generatedQuery.Explanation,
                    RelevantContexts = relevantDocuments.Select(d => new RelevantContext
                    {
                        Title = d.Title,
                        ContextType = d.ContentType,
                        Snippet = TruncateContent(d.Content, 200)
                    }).ToList(),
                    TokensUsed = new TokenUsage
                    {
                        InputTokens = generatedQuery.InputTokens,
                        OutputTokens = generatedQuery.OutputTokens,
                        TotalTokens = generatedQuery.InputTokens + generatedQuery.OutputTokens,
                        EmbeddingTokens = embeddingTokens,
                        TotalInQuery = tokensUsedInQuery
                    },
                    TokenLimit = richiesteDisponibili, // Usa il valore dal servizio licenze
                    TotalTokensUsed =  tokensUsedInQuery,
                    TokensRemaining = tokenResponse?.TokensRemaining ?? 0,//newTokensRemaining
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
                _logger.LogError(ex, "Errore nel processing della query RAG");
                throw;
            }
        }
        // Metodo helper per stimare i token
        private int EstimateTokenCount(string text)
        {
            var encoding = GptEncoding.GetEncodingForModel(_configuration["Azure:OpenAI:DeploymentName"]);
            int tokenCount = encoding.Encode(text).Count;
            return tokenCount;
            // Approssimazione semplice: ~4 caratteri per token
            // Per una stima più precisa, potresti usare una libreria tokenizer
            //return text.Length / 4;
        }
        // Esegue una query SQL su un'istanza client
        public async Task<List<dynamic>> ExecuteQueryAsync(
            string sqlQuery,
            string clientId,
            Dictionary<string, object> parameters = null)
        {
            try
            {
                // Ottieni connection string per il client
                string connectionString = GetConnectionStringForClient(clientId);

                // Esegui query (usando Dapper per semplicità)
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                // Converti parametri in DynamicParameters se presenti
                object paramObj = null;
                if (parameters != null && parameters.Count > 0)
                {
                    var dynamicParams = new DynamicParameters();
                    foreach (var param in parameters)
                    {
                        dynamicParams.Add(param.Key, param.Value);
                    }
                    paramObj = dynamicParams;
                }

                // Esegui la query
                var result = await connection.QueryAsync<dynamic>(sqlQuery, paramObj);
                return result.AsList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nell'esecuzione della query SQL");
                throw;
            }
        }
        private async Task<List<SchemaDocument>> RetrieveRelevantDocumentsAsync(
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

                // Configura ricerca vettoriale pura
                var searchOptions = new SearchOptions
                {
                    Filter = filter,
                    Size = 3,
                    Select = { "id", "clientId", "title", "content", "contentType", "module" }
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

                // Esegui ricerca (nota: passiamo una stringa vuota come query di testo)
                var searchResults = await _searchClient.SearchAsync<SchemaDocument>("", searchOptions);

                // Estrai documenti
                var documents = new List<SchemaDocument>();
                foreach (var result in searchResults.Value.GetResults())
                {
                    documents.Add(result.Document);
                }

                _logger.LogInformation("Recuperati {Count} documenti rilevanti per la query vettoriale", documents.Count);

                return documents;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nel recupero documenti rilevanti");
                throw;
            }
        }
        // Recupera documenti rilevanti dall'indice
        /*private async Task<List<SchemaDocument>> RetrieveRelevantDocumentsAsync(
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

                // Configura ricerca
                var searchOptions = new SearchOptions
                {
                    Filter = filter,
                    Size = 5, // Limita a 5 documenti rilevanti per ridurre token
                    QueryType = SearchQueryType.Semantic,
                    // SemanticConfigurationName = "default",
                    IncludeTotalCount = true
                };

                // Aggiungi select per ridurre dimensione risposta
                searchOptions.Select.Add("id");
                searchOptions.Select.Add("clientId");
                searchOptions.Select.Add("title");
                searchOptions.Select.Add("content");
                searchOptions.Select.Add("contentType");
                searchOptions.Select.Add("module");

                // Esegui ricerca
                var searchResults = await _searchClient.SearchAsync<SchemaDocument>(query, searchOptions);

                // Estrai documenti
                var documents = new List<SchemaDocument>();
                await foreach (var result in searchResults.Value.GetResultsAsync())
                {
                    documents.Add(result.Document);
                }

                _logger.LogInformation("Recuperati {Count} documenti rilevanti per la query", documents.Count);

                return documents;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nel recupero documenti rilevanti");
                throw;
            }
        }*/

        // Genera query SQL dall'input utente e contesto
        private async Task<GeneratedQuery> GenerateSqlQueryAsync(
            string query,
            List<SchemaDocument> relevantDocuments)
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
Sei un assistente esperto in sistemi ERP per il settore ortofrutticolo. Il tuo compito è generare query SQL corrette per SQL Server in base alla richiesta dell'utente.
Utilizza SOLO le informazioni contenute nel seguente schema del database e regole di business:

{contextText}

Genera una query SQL corretta che risponda alla richiesta dell'utente. Assicurati che:
1. I nomi di tabelle e campi siano corretti secondo lo schema fornito
2. Vengano utilizzati i join corretti in base alle relazioni descritte
3. Le condizioni WHERE siano appropriate
4. La query segua le best practices SQL Server
5. Vengano aggiunti commenti per spiegare passaggi complessi

Rispondi in formato JSON con questa struttura:
{{
  ""sql_query"": ""query SQL completa"",
  ""explanation"": ""spiegazione in italiano di come funziona la query""
}}";

                // Configura il client Azure OpenAI con le credenziali appropriate
                string endpoint = _configuration["Azure:OpenAI:Endpoint"];
                string apiKey = _configuration["Azure:OpenAI:Key"];
                string deploymentName = _configuration["Azure:OpenAI:DeploymentName"];

                // Usa il nuovo pattern per creare il client di Azure OpenAI
                var client = new AzureOpenAIClient(
                    new Uri(endpoint),
                    new ApiKeyCredential(apiKey));

                // Ottieni un ChatClient specifico per il deployment configurato
                var chatClient = client.GetChatClient(deploymentName);

                // Prepara la lista di messaggi per la chat
                var messages = new List<ChatMessage>
                    {
                        new SystemChatMessage(systemPrompt),
                        new UserChatMessage(query)
                    };

                // Configura le opzioni con temperatura bassa per risposte più deterministiche
                var chatCompletionOptions = new ChatCompletionOptions
                {
                    Temperature = 0.1f,
                    MaxOutputTokenCount = 1000
                };

                // Ottieni la risposta dall'API
                var response = await chatClient.CompleteChatAsync(messages, chatCompletionOptions);
                string responseContent = response.Value.Content[0].Text;

                // Calcola l'utilizzo di token (se necessario)
                int inputTokens = response.Value.Usage.InputTokenCount;
                int outputTokens = response.Value.Usage.OutputTokenCount;
                int totalTokens = response.Value.Usage.TotalTokenCount;

                try
                {
                    // Parse JSON response
                    var jsonResponse = JsonSerializer.Deserialize<GeneratedQueryJson>(responseContent);
                    string sqlQuery = jsonResponse.SqlQuery;

                    if (!ValidateSqlQuery(sqlQuery))
                    {
                        // Se la query non è valida, genera un errore o una query sicura predefinita
                        _logger.LogError("Query SQL non sicura generata dall'IA. Query originale: {OriginalQuery}", query);

                        return new GeneratedQuery
                        {
                            SqlQuery = "SELECT 'Query non sicura generata' AS Message",
                            Explanation = "La query generata dall'assistente AI è stata bloccata per motivi di sicurezza.",
                            InputTokens = inputTokens,
                            OutputTokens = outputTokens,
                            TotalTokens = totalTokens,
                            IsSafe = false
                        };
                    }
                    return new GeneratedQuery
                    {
                        SqlQuery = jsonResponse.SqlQuery,
                        Explanation = jsonResponse.Explanation,
                        InputTokens = inputTokens,     // Usa InputTokens invece di PromptTokens
                        OutputTokens = outputTokens,   // Usa OutputTokens invece di CompletionTokens
                        TotalTokens = totalTokens,      // Aggiungi TotalTokens
                        IsSafe = true

                    };
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Errore nel parsing JSON della risposta. Fallback su estrazione testuale.");

                    // Fallback per gestire risposte non in formato JSON
                    return new GeneratedQuery
                    {
                        SqlQuery = ExtractSqlFromText(responseContent),
                        Explanation = "Query generata dall'assistente AI.",
                        InputTokens = inputTokens,
                        OutputTokens = outputTokens,
                        TotalTokens = totalTokens
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nella generazione della query SQL");
                throw;
            }
        }
        private bool ValidateSqlQuery(string sqlQuery)
        {
            // Converti in minuscolo per controlli case-insensitive
            string queryLower = sqlQuery.ToLowerInvariant();

            // Lista di comandi potenzialmente pericolosi che vogliamo bloccare
            string[] dangerousCommands = new[] {
                "drop table", "drop database", "truncate table",
                "alter table", "delete from", "update ", "insert into",
                "exec ", "execute ", "sp_", "xp_",
                "create ", "alter ", "begin transaction", "commit", "rollback"
            };

            // Controlla se la query contiene comandi pericolosi
            foreach (var command in dangerousCommands)
            {
                if (queryLower.Contains(command))
                {
                    _logger.LogWarning("Query potenzialmente pericolosa rilevata. Contiene: {Command}", command);
                    return false;
                }
            }

            // Se la query contiene più di una istruzione (con punto e virgola)
            if (queryLower.Count(c => c == ';') > 1)
            {
                _logger.LogWarning("Query con più istruzioni rilevata, possibile pericolo");
                return false;
            }

            // Verifica che la query sia di tipo SELECT
            if (!queryLower.TrimStart().StartsWith("select "))
            {
                _logger.LogWarning("Query non SELECT rilevata");
                return false;
            }

            return true;
        }
        // Estrae SQL da testo non strutturato (fallback)
        private string ExtractSqlFromText(string text)
        {
            // Cerca blocchi di codice SQL
            var match = System.Text.RegularExpressions.Regex.Match(
                text,
                @"```sql\s*([\s\S]*?)\s*```",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }

            // Trova la prima occorrenza di SELECT
            match = System.Text.RegularExpressions.Regex.Match(
                text,
                @"(SELECT[\s\S]*?)(;|\z)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }

            // Fallback: ritorna il testo originale
            return text;
        }

        private string GetConnectionStringForClient(string clientId)
        {
            // Recupera la connection string per il client specifico
            // In produzione questo dovrebbe essere sicuro (Key Vault o altro)
            string connectionString = _configuration.GetConnectionString($"Client_{clientId}");

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ApplicationException($"Connection string non trovata per client {clientId}");
            }

            return connectionString;
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
