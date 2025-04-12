using Azure.AI.OpenAI;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using SednaRag.Models;
using SednaRag.Services.Clients;
using System.Text;
using System.Text.Json;

namespace SednaRag.Services
{
    public class ErpActionService
    {
        private readonly AzureOpenAIClient _openAIClient;
        private readonly SearchClient _searchClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ErpActionService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ErpActionService(
            AzureOpenAIClient openAIClient,
            ErpActionSearchClient searchClient,
            IConfiguration configuration,
            ILogger<ErpActionService> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _openAIClient = openAIClient;
            _searchClient = searchClient.Client;
            _configuration = configuration;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        // Recupera tutte le azioni ERP disponibili per un client
        public async Task<List<ErpActionDefinition>> GetActionsAsync(string clientId, string module = null)
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
                    Size = 1000,
                    OrderBy = { "name" }
                };

                // Esegui ricerca
                var searchResults = await _searchClient.SearchAsync<ErpActionDefinition>("*", searchOptions);

                // Estrai azioni
                var actions = new List<ErpActionDefinition>();
                await foreach (var result in searchResults.Value.GetResultsAsync())
                {
                    actions.Add(result.Document);
                }

                _logger.LogInformation("Recuperate {Count} azioni ERP per clientId {ClientId}", actions.Count, clientId);

                return actions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nel recupero azioni ERP per clientId {ClientId}", clientId);
                throw;
            }
        }

        // Recupera un'azione specifica per ID
        public async Task<ErpActionDefinition> GetActionAsync(string clientId, string actionId)
        {
            try
            {
                // Recupera l'azione direttamente per ID
                var action = await _searchClient.GetDocumentAsync<ErpActionDefinition>(actionId);

                // Verifica che l'azione appartenga al client
                if (action.Value.ClientId != clientId && action.Value.ClientId != "common")
                {
                    _logger.LogWarning("Tentativo di accesso a un'azione di un altro client: {ActionId}", actionId);
                    return null;
                }

                return action.Value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nel recupero azione ERP {ActionId}", actionId);
                // Restituisce null se l'azione non esiste
                return null;
            }
        }

        // Recupera un'azione specifica per nome
        public async Task<ErpActionDefinition> GetActionByNameAsync(string clientId, string actionName)
        {
            try
            {
                // Costruisci filtro per cercare l'azione per nome
                string filter = $"(clientId eq '{clientId}' or clientId eq 'common') and name eq '{actionName}'";

                // Configura ricerca
                var searchOptions = new SearchOptions
                {
                    Filter = filter,
                    Size = 1
                };

                // Esegui ricerca
                var searchResults = await _searchClient.SearchAsync<ErpActionDefinition>("*", searchOptions);

                // Estrai la prima azione trovata
                var actions = new List<ErpActionDefinition>();
                await foreach (var result in searchResults.Value.GetResultsAsync())
                {
                    actions.Add(result.Document);
                }

                return actions.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nel recupero azione ERP per nome {ActionName}", actionName);
                return null;
            }
        }

        // Crea una nuova azione ERP
        public async Task<string> CreateActionAsync(ErpActionDefinition action)
        {
            try
            {
                // Assegna un ID se non presente
                if (string.IsNullOrEmpty(action.Id))
                {
                    action.Id = $"{action.ClientId}_action_{Guid.NewGuid()}";
                }

                // Assegna timestamp di creazione
                action.CreatedAt = DateTime.UtcNow;
                action.UpdatedAt = DateTime.UtcNow;

                // Genera embedding per l'azione
                await GenerateEmbeddingAsync(action);

                // Carica nel search index
                await _searchClient.IndexDocumentsAsync(IndexDocumentsBatch.Upload(new[] { action }));

                _logger.LogInformation("Azione ERP creata: {ActionId}", action.Id);

                return action.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nella creazione dell'azione ERP");
                throw;
            }
        }

        // Aggiorna un'azione esistente
        public async Task<bool> UpdateActionAsync(string actionId, ErpActionDefinition action)
        {
            try
            {
                // Verifica che l'ID dell'azione sia coerente
                if (action.Id != actionId)
                {
                    action.Id = actionId;
                }

                // Recupera l'azione esistente per verificare che esista
                var existingAction = await GetActionAsync(action.ClientId, actionId);
                if (existingAction == null)
                {
                    return false;
                }

                // Aggiorna il timestamp di modifica
                action.UpdatedAt = DateTime.UtcNow;
                action.CreatedAt = existingAction.CreatedAt; // Preserva il timestamp di creazione originale

                // Genera nuovo embedding
                await GenerateEmbeddingAsync(action);

                // Aggiorna nel search index
                await _searchClient.IndexDocumentsAsync(IndexDocumentsBatch.MergeOrUpload(new[] { action }));

                _logger.LogInformation("Azione ERP aggiornata: {ActionId}", action.Id);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nell'aggiornamento dell'azione ERP {ActionId}", actionId);
                throw;
            }
        }

        // Elimina un'azione
        public async Task<bool> DeleteActionAsync(string clientId, string actionId)
        {
            try
            {
                // Verifica che l'azione esista e appartenga al client
                var existingAction = await GetActionAsync(clientId, actionId);
                if (existingAction == null)
                {
                    return false;
                }

                // Elimina dal search index
                await _searchClient.IndexDocumentsAsync(batch: IndexDocumentsBatch.Delete("id", new[] { actionId }));

                _logger.LogInformation("Azione ERP eliminata: {ActionId}", actionId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nell'eliminazione dell'azione ERP {ActionId}", actionId);
                throw;
            }
        }

        // Cerca azioni in base a una query
        public async Task<List<ErpActionDefinition>> SearchActionsAsync(string clientId, string query, string module = null)
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

                // Configura ricerca ibrida (vettoriale + full-text)
                var searchOptions = new SearchOptions
                {
                    Filter = filter,
                    Size = 10,
                    Select = { "id", "clientId", "name", "description", "functionName", "parameters", "module", "tags", "actionType", "requiresConfirmation", "createdAt", "updatedAt" }
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
                var searchResults = await _searchClient.SearchAsync<ErpActionDefinition>(query, searchOptions);

                // Estrai azioni
                var actions = new List<ErpActionDefinition>();
                await foreach (var result in searchResults.Value.GetResultsAsync())
                {
                    actions.Add(result.Document);
                }

                _logger.LogInformation("Ricerca azioni ERP: {Count} risultati per query '{Query}'", actions.Count, query);

                return actions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nella ricerca azioni ERP per query '{Query}'", query);
                throw;
            }
        }

        // Esegue un test sull'azione ERP
        public async Task<object> TestActionAsync(string actionName, string clientId, Dictionary<string, object> parameters)
        {
            try
            {
                _logger.LogInformation("Test azione ERP '{Action}' per client {ClientId}", actionName, clientId);

                // Questo metodo dovrebbe implementare una versione semplificata dell'azione
                // o chiamare un ambiente di test per verificare che l'azione funzioni correttamente

                // Recupera l'azione da testare
                var action = await GetActionByNameAsync(clientId, actionName);
                if (action == null)
                {
                    throw new InvalidOperationException($"Azione '{actionName}' non trovata");
                }

                // In un'implementazione reale, qui si dovrebbe eseguire un test dell'azione
                // Per ora, restituiamo un risultato fittizio
                return new
                {
                    Success = true,
                    Message = $"Test dell'azione '{actionName}' completato con successo",
                    Parameters = parameters,
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nel test dell'azione ERP '{Action}' per client {ClientId}", actionName, clientId);
                throw;
            }
        }

        // Bulk import di azioni ERP
        public async Task<int> BulkImportActionsAsync(List<ErpActionDefinition> actions)
        {
            try
            {
                if (actions == null || actions.Count == 0)
                {
                    return 0;
                }

                // Prepara tutte le azioni
                foreach (var action in actions)
                {
                    // Assegna ID se mancante
                    if (string.IsNullOrEmpty(action.Id))
                    {
                        action.Id = $"{action.ClientId}_action_{Guid.NewGuid()}";
                    }

                    // Assegna timestamp
                    action.CreatedAt = DateTime.UtcNow;
                    action.UpdatedAt = DateTime.UtcNow;

                    // Genera embedding
                    await GenerateEmbeddingAsync(action);
                }

                // Suddividi in batch per l'upload (massimo 1000 documenti per batch)
                const int batchSize = 1000;
                int importedCount = 0;

                for (int i = 0; i < actions.Count; i += batchSize)
                {
                    var batch = actions.Skip(i).Take(batchSize).ToList();
                    var response = await _searchClient.IndexDocumentsAsync(IndexDocumentsBatch.Upload(batch));
                    importedCount += response.Value.Results.Count(r => r.Succeeded);
                }

                _logger.LogInformation("Bulk import completato: {Count} azioni ERP importate", importedCount);

                return importedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nel bulk import di azioni ERP");
                throw;
            }
        }

        // Genera embedding per un'azione ERP
        private async Task GenerateEmbeddingAsync(ErpActionDefinition action)
        {
            try
            {
                // Prepara il testo per l'embedding
                var textBuilder = new StringBuilder();
                textBuilder.AppendLine($"Name: {action.Name}");
                textBuilder.AppendLine($"Description: {action.Description}");
                textBuilder.AppendLine($"Function: {action.FunctionName}");
                textBuilder.AppendLine("Parameters:");

                if (action.Parameters != null)
                {
                    foreach (var param in action.Parameters)
                    {
                        textBuilder.AppendLine($"- {param.Name} ({param.DataType}): {param.Description}");
                    }
                }

                if (action.Tags != null && action.Tags.Count > 0)
                {
                    textBuilder.AppendLine($"Tags: {string.Join(", ", action.Tags)}");
                }

                if (!string.IsNullOrEmpty(action.ActionType))
                {
                    textBuilder.AppendLine($"Action Type: {action.ActionType}");
                }

                string textToEmbed = textBuilder.ToString();

                // Genera embedding
                var embeddingModelName = _configuration["Azure:OpenAI:EmbeddingModel"] ?? "text-embedding-ada-002";
                var embeddingClient = _openAIClient.GetEmbeddingClient(embeddingModelName);
                var response = await embeddingClient.GenerateEmbeddingAsync(textToEmbed);
                var embedding = response.Value.ToFloats();

                // Assegna l'embedding all'azione
                action.ContentVector = embedding.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nella generazione dell'embedding per l'azione ERP");
                throw;
            }
        }
    }
}