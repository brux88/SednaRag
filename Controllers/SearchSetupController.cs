using Azure;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Microsoft.AspNetCore.Mvc;
using SednaRag.Helpers;
using System.Security.Cryptography;

namespace SednaRag.Controllers
{
    [ApiKeyAdmin]
    [ApiController]
    [Route("api/[controller]")]
    public class SearchSetupController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<SearchSetupController> _logger;

        public SearchSetupController(
            IConfiguration configuration,
            ILogger<SearchSetupController> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        // Endpoint per creare tutti gli indici necessari
        [HttpPost("create-all-indexes")]
        public async Task<IActionResult> CreateAllIndexes()
        {
            try
            {
                // Crea indice per lo schema RAG
                var schemaIndexResult = await CreateSchemaIndex();

                // Crea indice per i documenti di supporto
                var supportIndexResult = await CreateSupportDocumentsIndex();

                // Crea indice per le azioni ERP
                var erpIndexResult = await CreateErpActionsIndex();

                return Ok(new
                {
                    Success = true,
                    SchemaIndex = schemaIndexResult,
                    SupportIndex = supportIndexResult,
                    ErpActionsIndex = erpIndexResult
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nella creazione degli indici di ricerca");
                return StatusCode(500, new { Success = false, Error = ex.Message });
            }
        }

        // Endpoint per creare solo l'indice dello schema RAG
        [HttpPost("create-schema-index")]
        public async Task<IActionResult> CreateSchemaIndexEndpoint()
        {
            try
            {
                var result = await CreateSchemaIndex();
                return Ok(new { Success = true, Result = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nella creazione dell'indice per lo schema RAG");
                return StatusCode(500, new { Success = false, Error = ex.Message });
            }
        }

        // Endpoint per creare solo l'indice dei documenti di supporto
        [HttpPost("create-support-index")]
        public async Task<IActionResult> CreateSupportIndexEndpoint()
        {
            try
            {
                var result = await CreateSupportDocumentsIndex();
                return Ok(new { Success = true, Result = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nella creazione dell'indice per i documenti di supporto");
                return StatusCode(500, new { Success = false, Error = ex.Message });
            }
        }

        // Endpoint per creare solo l'indice delle azioni ERP
        [HttpPost("create-erp-actions-index")]
        public async Task<IActionResult> CreateErpActionsIndexEndpoint()
        {
            try
            {
                var result = await CreateErpActionsIndex();
                return Ok(new { Success = true, Result = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nella creazione dell'indice per le azioni ERP");
                return StatusCode(500, new { Success = false, Error = ex.Message });
            }
        }

        // Metodo per creare l'indice dello schema RAG
        private async Task<string> CreateSchemaIndex()
        {
            var endpoint = new Uri(_configuration["Azure:Search:Endpoint"]);
            var key = new AzureKeyCredential(_configuration["Azure:Search:Key"]);
            var indexName = _configuration["Azure:Search:Indexes:RagSchema"];

            var indexClient = new SearchIndexClient(endpoint, key);

            // Definizione dell'indice per lo schema RAG
            var index = new SearchIndex(indexName)
            {
                Fields = new[]
                {
                    new SearchField("id", SearchFieldDataType.String) { IsKey = true, IsFilterable = true },
                    new SearchField("clientId", SearchFieldDataType.String) { IsFilterable = true },
                    new SearchField("title", SearchFieldDataType.String) { IsSearchable = true, IsFilterable = true },
                    new SearchField("content", SearchFieldDataType.String) { IsSearchable = true },
                    new SearchField("contentType", SearchFieldDataType.String) { IsFilterable = true },
                    new SearchField("module", SearchFieldDataType.String) { IsFilterable = true },
                    new SearchField("keywords", SearchFieldDataType.Collection(SearchFieldDataType.String)) { IsFilterable = true },
                    new SearchField("contentVector", SearchFieldDataType.Collection(SearchFieldDataType.Single))
                    {
                        IsSearchable = true,
                        VectorSearchDimensions = 1536, // Dimensione standard per embeddings di OpenAI
                        VectorSearchProfileName = "schema-vector-config"
                    }
                },
                VectorSearch = new VectorSearch
                {
                    Profiles =
                    {
                        new VectorSearchProfile("schema-vector-config", "schema-vector-algorithm")
                    },
                    Algorithms =
                    {
                        new HnswAlgorithmConfiguration("schema-vector-algorithm")
                        {
                            Parameters = new HnswParameters
                            {
                                M = 4,  // Numero di connessioni
                                EfConstruction = 400,  // Precisione durante la costruzione
                                EfSearch = 500,  // Precisione durante la ricerca
                                Metric = VectorSearchAlgorithmMetric.Cosine
                            }
                        }
                    }
                }
            };

            try
            {
                // Crea o aggiorna l'indice
                var operation = await indexClient.CreateOrUpdateIndexAsync(index);
                _logger.LogInformation("Indice schema RAG creato: {IndexName}", indexName);
                return $"Indice {indexName} creato con successo";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nella creazione dell'indice per lo schema RAG: {IndexName}", indexName);
                throw;
            }
        }

        // Metodo per creare l'indice dei documenti di supporto
        private async Task<string> CreateSupportDocumentsIndex()
        {
            var endpoint = new Uri(_configuration["Azure:Search:Endpoint"]);
            var key = new AzureKeyCredential(_configuration["Azure:Search:Key"]);
            var indexName = _configuration["Azure:Search:Indexes:SupportDocs"];

            var indexClient = new SearchIndexClient(endpoint, key);

            // Definizione dell'indice per i documenti di supporto
            var index = new SearchIndex(indexName)
            {
                Fields = new[]
                {
                    new SearchField("id", SearchFieldDataType.String) { IsKey = true, IsFilterable = true },
                    new SearchField("clientId", SearchFieldDataType.String) { IsFilterable = true },
                    new SearchField("title", SearchFieldDataType.String) { IsSearchable = true, IsFilterable = true },
                    new SearchField("content", SearchFieldDataType.String) { IsSearchable = true },
                    new SearchField("contentVector", SearchFieldDataType.Collection(SearchFieldDataType.Single))
                    {
                        IsSearchable = true,
                        VectorSearchDimensions = 1536,
                        VectorSearchProfileName = "support-vector-config"
                    },
                    new SearchField("module", SearchFieldDataType.String) { IsFilterable = true },
                    new SearchField("tags", SearchFieldDataType.Collection(SearchFieldDataType.String)) { IsFilterable = true },
                    new SearchField("documentType", SearchFieldDataType.String) { IsFilterable = true },
                    new SearchField("createdAt", SearchFieldDataType.DateTimeOffset) { IsFilterable = true, IsSortable = true },
                    new SearchField("updatedAt", SearchFieldDataType.DateTimeOffset) { IsFilterable = true, IsSortable = true }
                },
                VectorSearch = new VectorSearch
                {
                    Profiles =
                    {
                        new VectorSearchProfile("support-vector-config", "support-vector-algorithm")
                    },
                    Algorithms =
                    {
                        new HnswAlgorithmConfiguration("support-vector-algorithm")
                        {
                            Parameters = new HnswParameters
                            {
                                M = 4,
                                EfConstruction = 400,
                                EfSearch = 500,
                                Metric = VectorSearchAlgorithmMetric.Cosine
                            }
                        }
                    }
                }
            };

            try
            {
                // Crea o aggiorna l'indice
                var operation = await indexClient.CreateOrUpdateIndexAsync(index);
                _logger.LogInformation("Indice documenti di supporto creato: {IndexName}", indexName);
                return $"Indice {indexName} creato con successo";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nella creazione dell'indice per i documenti di supporto: {IndexName}", indexName);
                throw;
            }
        }

        // Metodo per creare l'indice delle azioni ERP
        private async Task<string> CreateErpActionsIndex()
        {
            var endpoint = new Uri(_configuration["Azure:Search:Endpoint"]);
            var key = new AzureKeyCredential(_configuration["Azure:Search:Key"]);
            var indexName = _configuration["Azure:Search:Indexes:ErpActions"];

            var indexClient = new SearchIndexClient(endpoint, key);
   
      

            // Definizione dell'indice per le azioni ERP
            var index = new SearchIndex(indexName)
            {
                Fields = new[]
                {
                    new SearchField("id", SearchFieldDataType.String) { IsKey = true, IsFilterable = true },
                    new SearchField("clientId", SearchFieldDataType.String) { IsFilterable = true },
                    new SearchField("name", SearchFieldDataType.String) { IsSearchable = true, IsFilterable = true },
                    new SearchField("description", SearchFieldDataType.String) { IsSearchable = true },
                    new SearchField("functionName", SearchFieldDataType.String) { IsSearchable = true, IsFilterable = true },
                    new SearchField("assemblyName", SearchFieldDataType.String) { IsFilterable = true },
                   // new SearchField("parameters", SearchFieldDataType.) { IsSearchable = true },
                new ComplexField("parameters",true)
                {   
                    Fields =  {
                        new SimpleField("name", SearchFieldDataType.String) {  },
                        new SimpleField("dataType", SearchFieldDataType.String) { },
                        new SimpleField("description", SearchFieldDataType.String) { },
                        new SimpleField("required", SearchFieldDataType.Boolean) { IsFilterable = true },
                        new SimpleField("defaultValue", SearchFieldDataType.String) { } // Aggiunto il campo mancante

                    }
                },
 

                    new SearchField("contentVector", SearchFieldDataType.Collection(SearchFieldDataType.Single))
                    {
                        IsSearchable = true,
                        VectorSearchDimensions = 1536,
                        VectorSearchProfileName = "action-vector-config"
                    },
                    new SearchField("module", SearchFieldDataType.String) { IsFilterable = true },
                    new SearchField("tags", SearchFieldDataType.Collection(SearchFieldDataType.String)) { IsFilterable = true },
                    new SearchField("actionType", SearchFieldDataType.String) { IsFilterable = true },
                    new SearchField("requiresConfirmation", SearchFieldDataType.Boolean) { IsFilterable = true },
                    new SearchField("createdAt", SearchFieldDataType.DateTimeOffset) { IsFilterable = true, IsSortable = true },
                    new SearchField("updatedAt", SearchFieldDataType.DateTimeOffset) { IsFilterable = true, IsSortable = true }
                },
                VectorSearch = new VectorSearch
                {
                    Profiles =
                    {
                        new VectorSearchProfile("action-vector-config", "action-vector-algorithm")
                    },
                    Algorithms =
                    {
                        new HnswAlgorithmConfiguration("action-vector-algorithm")
                        {
                            Parameters = new HnswParameters
                            {
                                M = 4,
                                EfConstruction = 400,
                                EfSearch = 500,
                                Metric = VectorSearchAlgorithmMetric.Cosine
                            }
                        }
                    }
                }
            };

            try
            {
                // Crea o aggiorna l'indice
                var operation = await indexClient.CreateOrUpdateIndexAsync(index);
                _logger.LogInformation("Indice azioni ERP creato: {IndexName}", indexName);
                return $"Indice {indexName} creato con successo";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nella creazione dell'indice per le azioni ERP: {IndexName}", indexName);
                throw;
            }
        }
    }
}