using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SednaRag.Helpers;
using SednaRag.Models;
using SednaRag.Services;

namespace SednaRag.Controllers
{
    [ApiKeyAdmin]
    [ApiController]
    [Route("api/[controller]")]
    public class RagSchemaController : ControllerBase
    {
        private readonly SqlOperatorService _ragService;
        private readonly SchemaImportService _schemaImportService;
        private readonly ILogger<RagSchemaController> _logger;

        public RagSchemaController(
            SqlOperatorService ragService,
            SchemaImportService schemaImportService,
            ILogger<RagSchemaController> logger)
        {
            _ragService = ragService;
            _schemaImportService = schemaImportService;
            _logger = logger;
        }

        // Endpoint per importare schema e logiche di business
        [HttpPost("import-schema")]
        public async Task<IActionResult> ImportSchema([FromBody] SchemaImportRequest request)
        {
            try
            {
                if (request == null ||
                    request.Tables == null && request.StoredProcedures == null &&
                     request.BusinessRules == null && request.QueryExamples == null)
                {
                    return BadRequest("Dati di schema non validi");
                }

                _logger.LogInformation("Inizio importazione schema per client {ClientId}", request.ClientId);

                // Importa i dati forniti manualmente
                var result = await _schemaImportService.ImportSchemaAsync(
                    request.ClientId,
                    request.Tables,
                    request.StoredProcedures,
                    request.BusinessRules,
                    request.QueryExamples,
                    request.AdditionalMetadata);

                _logger.LogInformation("Importazione schema completata: {Count} documenti creati", result.DocumentsCreated);

                return Ok(new
                {
                    Success = true,
                    result.DocumentsCreated,
                    result.DocumentsUpdated,
                    Status = "Importazione schema completata"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nell'importazione schema");
                return StatusCode(500, new { Success = false, Error = ex.Message });
            }
        }

        // Endpoint per interrogare il sistema RAG
      /* [HttpPost("query")]
        public async Task<IActionResult> QueryRag([FromBody] RagQueryRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Query) || string.IsNullOrEmpty(request.ClientId))
                {
                    return BadRequest("Query e ClientId sono obbligatori");
                }

                _logger.LogInformation("Nuova richiesta RAG: {Query}", request.Query);

                // Processa la richiesta
                var result = await _ragService.ProcessQueryAsync(
                    request.Query,
                    request.ClientId,
                    request.Module);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nell'elaborazione query RAG");
                return StatusCode(500, new { Success = false, Error = ex.Message });
            }
        }*/

        // Endpoint per eseguire la query generata
       /* [HttpPost("execute")]
        public async Task<IActionResult> ExecuteQuery([FromBody] ExecuteQueryRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.SqlQuery) || string.IsNullOrEmpty(request.ClientId))
                {
                    return BadRequest("SqlQuery e ClientId sono obbligatori");
                }

                _logger.LogInformation("Esecuzione query per client {ClientId}: {Query}",
                    request.ClientId, request.SqlQuery);

                // Esegui la query
                var result = await _ragService.ExecuteQueryAsync(
                    request.SqlQuery,
                    request.ClientId,
                    request.Parameters);

                return Ok(new
                {
                    Success = true,
                    Data = result,
                    RowCount = result.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nell'esecuzione query");
                return StatusCode(500, new { Success = false, Error = ex.Message });
            }
        }*/

        // Endpoint per salvare un esempio di query utile
        [HttpPost("save-example")]
        public async Task<IActionResult> SaveQueryExample([FromBody] SaveQueryExampleRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Query) ||
                    string.IsNullOrEmpty(request.ClientId) ||
                    string.IsNullOrEmpty(request.Description))
                {
                    return BadRequest("Query, ClientId e Description sono obbligatori");
                }

                _logger.LogInformation("Salvataggio esempio di query: {Description}", request.Description);

                // Crea esempio di query
                var queryExample = new QueryExample
                {
                    Description = request.Description,
                    SqlQuery = request.SqlQuery,
                    Explanation = request.Explanation,
                    UseCase = request.UseCase,
                    Module = request.Module,
                    Keywords = request.Keywords
                };

                // Aggiungi all'indice
                await _schemaImportService.AddQueryExampleAsync(request.ClientId, queryExample);

                return Ok(new { Success = true, Message = "Esempio di query salvato con successo" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nel salvataggio dell'esempio di query");
                return StatusCode(500, new { Success = false, Error = ex.Message });
            }
        }

        // Endpoint per test di connessione
        [HttpGet("ping")]
        public IActionResult Ping()
        {
            return Ok(new
            {
                Status = "OK",
                Timestamp = DateTime.UtcNow,
                Version = "1.0.0"
            });
        }
    }
}
