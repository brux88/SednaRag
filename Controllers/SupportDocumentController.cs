using Microsoft.AspNetCore.Mvc;
using SednaRag.Helpers;
using SednaRag.Models;
using SednaRag.Services;

namespace SednaRag.Controllers
{
    [ApiKeyAdmin]
    [ApiController]
    [Route("api/[controller]")]
    public class SupportDocumentController : ControllerBase
    {
        private readonly SupportDocumentService _documentService;
        private readonly ILogger<SupportDocumentController> _logger;

        public SupportDocumentController(
            SupportDocumentService documentService,
            ILogger<SupportDocumentController> logger)
        {
            _documentService = documentService;
            _logger = logger;
        }

        // Endpoint per ottenere tutti i documenti di supporto per un client
        [HttpGet("{clientId}")]
        public async Task<IActionResult> GetAllDocuments(string clientId, [FromQuery] string module = null)
        {
            try
            {
                var documents = await _documentService.GetDocumentsAsync(clientId, module);
                return Ok(documents);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nel recupero dei documenti di supporto");
                return StatusCode(500, new { Success = false, Error = ex.Message });
            }
        }

        // Endpoint per ottenere un documento specifico
        [HttpGet("{clientId}/{documentId}")]
        public async Task<IActionResult> GetDocument(string clientId, string documentId)
        {
            try
            {
                var document = await _documentService.GetDocumentAsync(clientId, documentId);
                if (document == null)
                {
                    return NotFound(new { Success = false, Error = "Documento non trovato" });
                }
                return Ok(document);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nel recupero del documento di supporto");
                return StatusCode(500, new { Success = false, Error = ex.Message });
            }
        }

        // Endpoint per creare un nuovo documento di supporto
        [HttpPost]
        public async Task<IActionResult> CreateDocument([FromBody] SupportDocument document)
        {
            try
            {
                if (document == null)
                {
                    return BadRequest("Documento non valido");
                }

                if (string.IsNullOrEmpty(document.ClientId) || string.IsNullOrEmpty(document.Title) || string.IsNullOrEmpty(document.Content))
                {
                    return BadRequest("ClientId, Titolo e Contenuto sono obbligatori");
                }

                _logger.LogInformation("Creazione nuovo documento di supporto per client {ClientId}: {Title}", document.ClientId, document.Title);

                var result = await _documentService.CreateDocumentAsync(document);
                return Ok(new { Success = true, DocumentId = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nella creazione del documento di supporto");
                return StatusCode(500, new { Success = false, Error = ex.Message });
            }
        }

        // Endpoint per aggiornare un documento esistente
        [HttpPut("{documentId}")]
        public async Task<IActionResult> UpdateDocument(string documentId, [FromBody] SupportDocument document)
        {
            try
            {
                if (document == null)
                {
                    return BadRequest("Documento non valido");
                }

                if (string.IsNullOrEmpty(document.ClientId) || string.IsNullOrEmpty(document.Title) || string.IsNullOrEmpty(document.Content))
                {
                    return BadRequest("ClientId, Titolo e Contenuto sono obbligatori");
                }

                _logger.LogInformation("Aggiornamento documento di supporto {DocumentId}", documentId);

                var result = await _documentService.UpdateDocumentAsync(documentId, document);
                if (!result)
                {
                    return NotFound(new { Success = false, Error = "Documento non trovato" });
                }

                return Ok(new { Success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nell'aggiornamento del documento di supporto");
                return StatusCode(500, new { Success = false, Error = ex.Message });
            }
        }

        // Endpoint per eliminare un documento
        [HttpDelete("{clientId}/{documentId}")]
        public async Task<IActionResult> DeleteDocument(string clientId, string documentId)
        {
            try
            {
                _logger.LogInformation("Eliminazione documento di supporto {DocumentId} per client {ClientId}", documentId, clientId);

                var result = await _documentService.DeleteDocumentAsync(clientId, documentId);
                if (!result)
                {
                    return NotFound(new { Success = false, Error = "Documento non trovato" });
                }

                return Ok(new { Success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nell'eliminazione del documento di supporto");
                return StatusCode(500, new { Success = false, Error = ex.Message });
            }
        }

        // Endpoint per cercare documenti di supporto
        [HttpGet("search/{clientId}")]
        public async Task<IActionResult> SearchDocuments(string clientId, [FromQuery] string query, [FromQuery] string module = null)
        {
            try
            {
                if (string.IsNullOrEmpty(query))
                {
                    return BadRequest("Query di ricerca obbligatoria");
                }

                _logger.LogInformation("Ricerca documenti di supporto per client {ClientId}: {Query}", clientId, query);

                var results = await _documentService.SearchDocumentsAsync(clientId, query, module);
                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nella ricerca dei documenti di supporto");
                return StatusCode(500, new { Success = false, Error = ex.Message });
            }
        }
    }
}