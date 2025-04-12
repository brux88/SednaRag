using Azure.AI.OpenAI;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Caching.Distributed;
using OpenAI.Chat;
using SednaRag.Models;
using System.Text;
using System.Text.Json;

namespace SednaRag.Services
{
    public class AgentOrchestratorService
    {
        private readonly SqlOperatorService _ragService;
        private readonly SupportAgentService _supportService;
        private readonly ErpOperatorService _erpService;
        private readonly AzureOpenAIClient _openAIClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AgentOrchestratorService> _logger;
        private readonly IDistributedCache _cache;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly HttpClient _httpClient;
        private readonly ErpActionService _actionService;

        public AgentOrchestratorService(
            SqlOperatorService ragService,
            SupportAgentService supportService,
            ErpOperatorService erpService,
            ErpActionService actionService,
            AzureOpenAIClient openAIClient,
            IConfiguration configuration,
            ILogger<AgentOrchestratorService> logger,
            IDistributedCache cache,
            IHttpContextAccessor httpContextAccessor,
            IHttpClientFactory httpClientFactory)
        {
            _ragService = ragService;
            _supportService = supportService;
            _erpService = erpService;
            _actionService = actionService;
            _openAIClient = openAIClient;
            _configuration = configuration;
            _logger = logger;
            _cache = cache;
            _httpContextAccessor = httpContextAccessor;
            _httpClient = httpClientFactory.CreateClient("LicenseService");
        }

        // Processa una query decidendo quale agente utilizzare
        public async Task<AssistantResponse> ProcessQueryAsync(
            string query,
            string clientId,
            string module = null,
            AssistantContext context = null)
        {
            string apiKey = _httpContextAccessor.HttpContext.Items["ApiKey"] as string;
            int richiesteDisponibili = _httpContextAccessor.HttpContext.Items["RichiesteDisponibili"] != null
                ? (int)_httpContextAccessor.HttpContext.Items["RichiesteDisponibili"]
                : 0;
            int intentTokens = 0;
            string agentType = "unknown";

            try
            {
                // Ottieni l'API key e le altre informazioni dal contesto della richiesta

                if (richiesteDisponibili <= 0)
                {
                    throw new InvalidOperationException("Limite di richieste AI raggiunto. Acquista ulteriori crediti.");
                }

                // Prima determina quale agente utilizzare
                var agentResult = await DetermineAgentAsync(query, clientId, module, context);
                agentType = agentResult.AgentType;
                intentTokens = agentResult.TokensUsed;

                _logger.LogInformation("Agente selezionato per la query: {AgentType}", agentType);

                // Esegui l'agente appropriato
                object agentResponse = null;
                int agentTokensUsed = 0;
                string responseText = "";
                List<SuggestedAction> suggestedActions = new List<SuggestedAction>();

                switch (agentType)
                {
                    case "rag":
                        var ragResponse = await _ragService.ProcessQueryAsync(query, clientId, module);
                        agentResponse = ragResponse;
                        agentTokensUsed = ragResponse.TokensUsed.TotalInQuery;
                        responseText = $"Ho generato la seguente query SQL basata sulla tua richiesta:\n\n```sql\n{ragResponse.QueryGenerated}\n```\n\n{ragResponse.Explanation}";

                        // Aggiungi azioni suggerite per eseguire la query
                        suggestedActions.Add(new SuggestedAction
                        {
                            Title = "Esegui questa query",
                            Description = "Esegui la query SQL generata nel database",
                            ActionType = "rag-execute",
                            Parameters = new Dictionary<string, object>
                            {
                                { "sqlQuery", ragResponse.QueryGenerated },
                                { "clientId", clientId }
                            }
                        });
                        break;

                    case "support":
                        var supportResponse = await _supportService.ProcessQueryAsync(query, clientId, module);
                        agentResponse = supportResponse;
                        agentTokensUsed = supportResponse.TokensUsed.TotalInQuery;
                        responseText = supportResponse.Response;

                        // Aggiungi azioni suggerite basate sui documenti pertinenti
                        if (supportResponse.RelevantDocuments?.Count > 0)
                        {
                            suggestedActions.Add(new SuggestedAction
                            {
                                Title = "Consulta la documentazione",
                                Description = $"Vedi il documento: {supportResponse.RelevantDocuments[0].Title}",
                                ActionType = "support-doc",
                                ActionId = supportResponse.RelevantDocuments[0].Id
                            });
                        }
                        break;

                    case "erp":
                        // Per le azioni ERP, determina prima l'azione specifica
                        var erpActionResult = await DetermineErpActionAsync(query, clientId, module, context);
                        string actionName = erpActionResult.ActionName;
                        var parameters = erpActionResult.Parameters;

                        var actionDefinition = await _actionService.GetActionByNameAsync(clientId, actionName);
                        if (actionDefinition != null)
                        {
                            // Se l'azione richiede conferma, non eseguirla automaticamente
                            if (actionDefinition.RequiresConfirmation)
                            {
                                responseText = $"Vuoi che esegua l'azione '{actionName}'? Questa operazione {actionDefinition.Description.ToLower()}.";

                                suggestedActions.Add(new SuggestedAction
                                {
                                    Title = $"Esegui: {actionName}",
                                    Description = actionDefinition.Description,
                                    ActionType = "erp-action",
                                    ActionId = actionName,
                                    Parameters = parameters
                                });
                            }
                            else
                            {
                                // Esegui direttamente l'azione
                                var erpResponse = await _erpService.ExecuteActionAsync(actionName, clientId, parameters);
                                agentResponse = erpResponse;

                                if (erpResponse.Success)
                                {
                                    responseText = $"Ho eseguito l'azione '{actionName}' con successo. {erpResponse.Message}";
                                }
                                else
                                {
                                    responseText = $"Non sono riuscito a eseguire l'azione '{actionName}'. Errore: {erpResponse.Error}";
                                }
                            }
                        }
                        else
                        {
                            responseText = $"Non ho trovato un'azione chiamata '{actionName}'. Posso aiutarti in altro modo?";
                        }

                        agentTokensUsed = erpActionResult.TokensUsed;
                        break;

                    default:
                        // Caso in cui non è stato possibile determinare l'agente
                        responseText = "Non sono sicuro di come aiutarti con questa richiesta. Puoi essere più specifico?";
                        break;
                }

                // Calcola token totali utilizzati
                int totalTokensUsed = intentTokens + agentTokensUsed;

                // Aggiorna il conteggio dei token nel servizio licenze
                var tokenUpdateRequest = new TokenUsageRequest
                {
                    ApiKey = apiKey,
                    TokensUsed = totalTokensUsed,
                    Module = module,
                    PromptType = "Assistant Query",
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
                var response = new AssistantResponse
                {
                    Success = true,
                    Response = responseText,
                    AgentUsed = agentType,
                    SuggestedActions = suggestedActions,
                    TokensUsed = new TokenUsage
                    {
                        InputTokens = intentTokens,
                        OutputTokens = agentTokensUsed,
                        TotalTokens = totalTokensUsed
                    },
                    TokensRemaining = tokenResponse?.TokensRemaining ?? 0
                };

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nel processing della query all'assistente");

                // Anche in caso di errore, aggiorna i token utilizzati per l'intent
                if (intentTokens > 0)
                {
                    try
                    {
                        var tokenUpdateRequest = new TokenUsageRequest
                        {
                            ApiKey = apiKey,
                            TokensUsed = intentTokens,
                            Module = module,
                            PromptType = "Assistant Intent Detection",
                            QueryText = query
                        };

                        _httpClient.DefaultRequestHeaders.Clear();
                        _httpClient.DefaultRequestHeaders.Add("ApiKey", $"{apiKey}");

                        var updateResponse = await _httpClient.PostAsJsonAsync(
                            $"updateTokenAI/{apiKey}",
                            tokenUpdateRequest);

                        if (!updateResponse.IsSuccessStatusCode)
                        {
                            _logger.LogError("Errore durante l'aggiornamento del conteggio token intent: {StatusCode}", updateResponse.StatusCode);
                        }
                    }
                    catch (Exception tokenEx)
                    {
                        _logger.LogError(tokenEx, "Errore nell'aggiornamento dei token di intent dopo un errore dell'agente");
                    }
                }

                return new AssistantResponse
                {
                    Success = false,
                    AgentUsed = agentType,
                    Error = ex.Message,
                    TokensUsed = new TokenUsage
                    {
                        InputTokens = intentTokens,
                        OutputTokens = 0,
                        TotalTokens = intentTokens
                    }
                };
            }
        }

        // Determina quale agente utilizzare per la query
        private async Task<(string AgentType, int TokensUsed)> DetermineAgentAsync(
            string query,
            string clientId,
            string module,
            AssistantContext context)
        {
            try
            {
                string systemPrompt = @"
Sei un Orchestratore di Agenti specializzato per un sistema ERP nel settore ortofrutticolo. Il tuo compito è analizzare l'intento dell'utente e determinare quale dei seguenti agenti è più appropriato per rispondere:

1. RAG SQL Generator - Usa questo agente quando l'utente vuole interrogare il database, ottenere dati, fare analisi su vendite, acquisti, inventario, ecc. Questo agente genera query SQL.

2. Support Agent - Usa questo agente quando l'utente ha domande su come utilizzare il software, richiede assistenza tecnica, o vuole informazioni sulle funzionalità dell'ERP.

3. ERP Operator - Usa questo agente quando l'utente vuole eseguire un'azione operativa nel sistema ERP, come creare un documento, modificare dati, o avviare un processo.

Analizza attentamente la richiesta dell'utente e determina l'intento principale. Rispondi SOLO con il nome dell'agente da utilizzare in questo formato:
{""agent"": ""rag"" | ""support"" | ""erp""}";

                // Se c'è un contesto, lo includiamo nel prompt
                string contextPrompt = "";
                if (context != null)
                {
                    contextPrompt = "\n\nContesto della conversazione:";

                    // Aggiungi la cronologia delle conversazioni
                    if (context.History != null && context.History.Count > 0)
                    {
                        contextPrompt += "\nStorico conversazione:";
                        foreach (var message in context.History.TakeLast(5)) // Ultime 5 messaggi
                        {
                            contextPrompt += $"\n{message.Role}: {message.Content}";
                        }
                    }

                    // Aggiungi informazioni sulla schermata corrente
                    if (!string.IsNullOrEmpty(context.CurrentScreen))
                    {
                        contextPrompt += $"\nSchermata corrente: {context.CurrentScreen}";
                    }

                    // Aggiungi informazioni sui dati selezionati
                    if (context.SelectedData != null && context.SelectedData.Count > 0)
                    {
                        contextPrompt += "\nDati selezionati:";
                        foreach (var item in context.SelectedData)
                        {
                            contextPrompt += $"\n- {item.Key}: {item.Value}";
                        }
                    }
                }

                // Configura il client di chat
                string deploymentName = _configuration["Azure:OpenAI:DeploymentName"];
                var chatClient = _openAIClient.GetChatClient(deploymentName);

                // Prepara la lista di messaggi per la chat
                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage(systemPrompt + contextPrompt),
                    new UserChatMessage(query)
                };

                // Configura le opzioni con temperatura bassa per risposte più deterministiche
                var chatCompletionOptions = new ChatCompletionOptions
                {
                    Temperature = 0.1f,
                    MaxOutputTokenCount = 100
                };

                // Ottieni la risposta dall'API
                var response = await chatClient.CompleteChatAsync(messages, chatCompletionOptions);
                string responseContent = response.Value.Content[0].Text;

                // Calcola l'utilizzo di token
                int tokensUsed = response.Value.Usage.TotalTokenCount;

                // Estrai l'agente dalla risposta
                try
                {
                    var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
                    if (jsonResponse.TryGetProperty("agent", out var agentElement))
                    {
                        string agent = agentElement.GetString().ToLowerInvariant();
                        if (agent == "rag" || agent == "support" || agent == "erp")
                        {
                            return (agent, tokensUsed);
                        }
                    }
                }
                catch (JsonException)
                {
                    // Fallback: cerca l'agente nel testo della risposta
                    responseContent = responseContent.ToLowerInvariant();
                    if (responseContent.Contains("rag"))
                    {
                        return ("rag", tokensUsed);
                    }
                    else if (responseContent.Contains("support"))
                    {
                        return ("support", tokensUsed);
                    }
                    else if (responseContent.Contains("erp"))
                    {
                        return ("erp", tokensUsed);
                    }
                }

                // Default a supporto se non è stato possibile determinare l'agente
                return ("support", tokensUsed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nella determinazione dell'agente");
                throw;
            }
        }

        // Determina quale azione ERP eseguire e con quali parametri
        private async Task<(string ActionName, Dictionary<string, object> Parameters, int TokensUsed)> DetermineErpActionAsync(
            string query,
            string clientId,
            string module,
            AssistantContext context)
        {
            try
            {
                // Recupera tutte le azioni disponibili per questo client
                var availableActions = await _actionService.GetActionsAsync(clientId, module);

                // Crea un prompt con la descrizione delle azioni disponibili
                var actionsDescription = new StringBuilder();
                foreach (var action in availableActions)
                {
                    actionsDescription.AppendLine($"- {action.Name}: {action.Description}");
                    actionsDescription.AppendLine("  Parametri:");
                    foreach (var param in action.Parameters)
                    {
                        actionsDescription.AppendLine($"  - {param.Name} ({param.DataType}): {param.Description}" + (param.Required ? " [OBBLIGATORIO]" : ""));
                    }
                    actionsDescription.AppendLine();
                }

                string systemPrompt = $@"
Sei un assistente specializzato per un sistema ERP nel settore ortofrutticolo. Il tuo compito è determinare quale azione ERP l'utente vuole eseguire e quali parametri deve utilizzare.

Azioni disponibili:
{actionsDescription}

Analizza attentamente la richiesta dell'utente e determina l'azione da eseguire e i parametri necessari. Rispondi in formato JSON con questo schema:
{{
  ""action"": ""nome_azione"",
  ""parameters"": {{
    ""param1"": valore1,
    ""param2"": valore2,
    ...
  }}
}}";

                // Aggiungi contesto se disponibile
                string contextPrompt = "";
                if (context != null)
                {
                    // Simile al metodo DetermineAgentAsync
                    if (context.SelectedData != null && context.SelectedData.Count > 0)
                    {
                        contextPrompt += "\nDati selezionati che possono essere usati come parametri:";
                        foreach (var item in context.SelectedData)
                        {
                            contextPrompt += $"\n- {item.Key}: {item.Value}";
                        }
                    }
                }

                // Configura il client di chat
                string deploymentName = _configuration["Azure:OpenAI:DeploymentName"];
                var chatClient = _openAIClient.GetChatClient(deploymentName);

                // Prepara la lista di messaggi per la chat
                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage(systemPrompt + contextPrompt),
                    new UserChatMessage(query)
                };

                // Configura le opzioni con temperatura bassa per risposte più deterministiche
                var chatCompletionOptions = new ChatCompletionOptions
                {
                    Temperature = 0.1f,
                    MaxOutputTokenCount = 500
                };

                // Ottieni la risposta dall'API
                var response = await chatClient.CompleteChatAsync(messages, chatCompletionOptions);
                string responseContent = response.Value.Content[0].Text;

                // Calcola l'utilizzo di token
                int tokensUsed = response.Value.Usage.TotalTokenCount;

                // Estrai l'azione e i parametri dalla risposta
                try
                {
                    var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);

                    if (jsonResponse.TryGetProperty("action", out var actionElement))
                    {
                        string actionName = actionElement.GetString();

                        Dictionary<string, object> parameters = new Dictionary<string, object>();
                        if (jsonResponse.TryGetProperty("parameters", out var paramsElement) && paramsElement.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var property in paramsElement.EnumerateObject())
                            {
                                parameters[property.Name] = ExtractValueFromJsonElement(property.Value);
                            }
                        }

                        return (actionName, parameters, tokensUsed);
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Errore nel parsing del JSON per la determinazione dell'azione ERP");
                }

                // Default se non è stato possibile determinare l'azione
                return ("unknown", new Dictionary<string, object>(), tokensUsed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nella determinazione dell'azione ERP");
                throw;
            }
        }

        // Helper per estrarre valori da JsonElement
        private object ExtractValueFromJsonElement(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    return element.GetString();
                case JsonValueKind.Number:
                    if (element.TryGetInt32(out int intValue))
                        return intValue;
                    if (element.TryGetInt64(out long longValue))
                        return longValue;
                    return element.GetDouble();
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                case JsonValueKind.Null:
                    return null;
                case JsonValueKind.Object:
                    var obj = new Dictionary<string, object>();
                    foreach (var property in element.EnumerateObject())
                    {
                        obj[property.Name] = ExtractValueFromJsonElement(property.Value);
                    }
                    return obj;
                case JsonValueKind.Array:
                    var arr = new List<object>();
                    foreach (var item in element.EnumerateArray())
                    {
                        arr.Add(ExtractValueFromJsonElement(item));
                    }
                    return arr;
                default:
                    return null;
            }
        }
    }
}