using Azure.AI.OpenAI;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using SednaRag.Models;
using SednaRag.Services.Clients;
using System.Text;
using System.Text.Json;

namespace SednaRag.Services
{
    public class SupportDocumentService
    {
        private readonly AzureOpenAIClient _openAIClient;
        private readonly SearchClient _searchClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<SupportDocumentService> _logger;

        public SupportDocumentService(
            AzureOpenAIClient openAIClient,
            SupportSearchClient searchClientWrapper,
            IConfiguration configuration,
            ILogger<SupportDocumentService> logger)
        {
            _openAIClient = openAIClient;
            _searchClient = searchClientWrapper.Client;
            _configuration = configuration;
            _logger = logger;
        }

        // Recupera tutti i documenti di supporto per un client
        public async Task<List<SupportDocument>> GetDocumentsAsync(string clientId, string module = null)
        {
            try
            {
                // Costruisci filtro
                string filter = $"clientId eq '{clientId}'";
                if (!string.IsNullOrEmpty(module))
                {
                    filter += $" and (module eq '{module}' or module eq 'all')";
                }

                // Configura ricerca
                var searchOptions = new SearchOptions
                {
                    Filter = filter,
                    Size = 1000,
                    OrderBy = { "title" }
                };

                // Esegui ricerca
                var searchResults = await _searchClient.SearchAsync<SupportDocument>("*", searchOptions);

                // Estrai documenti
                var documents = new List<SupportDocument>();
                await foreach (var result in searchResults.Value.GetResultsAsync())
                {
                    documents.Add(result.Document);
                }

                _logger.LogInformation("Recuperati {Count} documenti di supporto per clientId {ClientId}", documents.Count, clientId);

                return documents;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nel recupero documenti di supporto per clientId {ClientId}", clientId);
                throw;
            }
        }

        // Recupera un documento specifico
        public async Task<SupportDocument> GetDocumentAsync(string clientId, string documentId)
        {
            try
            {
                // Recupera il documento direttamente per ID
                var document = await _searchClient.GetDocumentAsync<SupportDocument>(documentId);

                // Verifica che il documento appartenga al client
                if (document.Value.ClientId != clientId && document.Value.ClientId != "common")
                {
                    _logger.LogWarning("Tentativo di accesso a documento di un altro client: {DocumentId}", documentId);
                    return null;
                }

                return document.Value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nel recupero documento di supporto {DocumentId}", documentId);
                // Restituisce null se il documento non esiste
                return null;
            }
        }

        // Crea un nuovo documento di supporto
        public async Task<string> CreateDocumentAsync(SupportDocument document)
        {
            try
            {
                // Assegna un ID se non presente
                if (string.IsNullOrEmpty(document.Id))
                {
                    document.Id = $"{document.ClientId}_support_{Guid.NewGuid()}";
                }

                // Assegna timestamp di creazione
                document.CreatedAt = DateTime.UtcNow;
                document.UpdatedAt = DateTime.UtcNow;

                // Genera embedding per il documento
                await GenerateEmbeddingAsync(document);

                // Carica nel search index
                await _searchClient.IndexDocumentsAsync(IndexDocumentsBatch.Upload(new[] { document }));

                _logger.LogInformation("Documento di supporto creato: {DocumentId}", document.Id);

                return document.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nella creazione del documento di supporto");
                throw;
            }
        }

        // Aggiorna un documento esistente
        public async Task<bool> UpdateDocumentAsync(string documentId, SupportDocument document)
        {
            try
            {
                // Verifica che l'ID del documento sia coerente
                if (document.Id != documentId)
                {
                    document.Id = documentId;
                }

                // Recupera il documento esistente per verificare che esista
                var existingDoc = await GetDocumentAsync(document.ClientId, documentId);
                if (existingDoc == null)
                {
                    return false;
                }

                // Aggiorna il timestamp di modifica
                document.UpdatedAt = DateTime.UtcNow;
                document.CreatedAt = existingDoc.CreatedAt; // Preserva il timestamp di creazione originale

                // Genera nuovo embedding
                await GenerateEmbeddingAsync(document);

                // Aggiorna nel search index
                await _searchClient.IndexDocumentsAsync(IndexDocumentsBatch.MergeOrUpload(new[] { document }));

                _logger.LogInformation("Documento di supporto aggiornato: {DocumentId}", document.Id);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nell'aggiornamento del documento di supporto {DocumentId}", documentId);
                throw;
            }
        }

        // Elimina un documento
        public async Task<bool> DeleteDocumentAsync(string clientId, string documentId)
        {
            try
            {
                // Verifica che il documento esista e appartenga al client
                var existingDoc = await GetDocumentAsync(clientId, documentId);
                if (existingDoc == null)
                {
                    return false;
                }

                // Elimina dal search index
                await _searchClient.IndexDocumentsAsync(batch: IndexDocumentsBatch.Delete("id", new[] { documentId }));

                _logger.LogInformation("Documento di supporto eliminato: {DocumentId}", documentId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nell'eliminazione del documento di supporto {DocumentId}", documentId);
                throw;
            }
        }

        // Cerca documenti in base a una query
        public async Task<List<SupportDocument>> SearchDocumentsAsync(string clientId, string query, string module = null)
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
                    Select = { "id", "clientId", "title", "content", "documentType", "module", "tags", "createdAt", "updatedAt" }
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
                var searchResults = await _searchClient.SearchAsync<SupportDocument>(query, searchOptions);

                // Estrai documenti
                var documents = new List<SupportDocument>();
                await foreach (var result in searchResults.Value.GetResultsAsync())
                {
                    documents.Add(result.Document);
                }

                _logger.LogInformation("Ricerca documenti di supporto: {Count} risultati per query '{Query}'", documents.Count, query);

                return documents;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nella ricerca documenti di supporto per query '{Query}'", query);
                throw;
            }
        }

        // Genera embedding per un documento
        private async Task GenerateEmbeddingAsync(SupportDocument document)
        {
            try
            {
                // Prepara il testo per l'embedding combinando titolo e contenuto
                string textToEmbed = document.Title + "\n\n" + document.Content;
                if (document.Tags != null && document.Tags.Count > 0)
                {
                    textToEmbed += "\n\nTags: " + string.Join(", ", document.Tags);
                }

                // Genera embedding
                var embeddingModelName = _configuration["Azure:OpenAI:EmbeddingModel"] ?? "text-embedding-ada-002";
                var embeddingClient = _openAIClient.GetEmbeddingClient(embeddingModelName);
                var response = await embeddingClient.GenerateEmbeddingAsync(textToEmbed);
                var embedding = response.Value.ToFloats();

                // Assegna l'embedding al documento
                document.ContentVector = embedding.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nella generazione dell'embedding per il documento di supporto");
                throw;
            }
        }

        // Bulk import di documenti di supporto
        public async Task<int> BulkImportDocumentsAsync(List<SupportDocument> documents)
        {
            try
            {
                if (documents == null || documents.Count == 0)
                {
                    return 0;
                }

                // Prepara tutti i documenti
                foreach (var document in documents)
                {
                    // Assegna ID se mancante
                    if (string.IsNullOrEmpty(document.Id))
                    {
                        document.Id = $"{document.ClientId}_support_{Guid.NewGuid()}";
                    }

                    // Assegna timestamp
                    document.CreatedAt = DateTime.UtcNow;
                    document.UpdatedAt = DateTime.UtcNow;

                    // Genera embedding
                    await GenerateEmbeddingAsync(document);
                }

                // Suddividi in batch per l'upload (massimo 1000 documenti per batch)
                const int batchSize = 1000;
                int importedCount = 0;

                for (int i = 0; i < documents.Count; i += batchSize)
                {
                    var batch = documents.Skip(i).Take(batchSize).ToList();
                    var response = await _searchClient.IndexDocumentsAsync(IndexDocumentsBatch.Upload(batch));
                    importedCount += response.Value.Results.Count(r => r.Succeeded);
                }

                _logger.LogInformation("Bulk import completato: {Count} documenti di supporto importati", importedCount);

                return importedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nel bulk import di documenti di supporto");
                throw;
            }
        }
    }
}