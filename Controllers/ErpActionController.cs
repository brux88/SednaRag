using Microsoft.AspNetCore.Mvc;
using SednaRag.Helpers;
using SednaRag.Models;
using SednaRag.Services;

namespace SednaRag.Controllers
{
    [ApiKeyAdmin]
    [ApiController]
    [Route("api/[controller]")]
    public class ErpActionController : ControllerBase
    {
        private readonly ErpActionService _actionService;
        private readonly ILogger<ErpActionController> _logger;

        public ErpActionController(
            ErpActionService actionService,
            ILogger<ErpActionController> logger)
        {
            _actionService = actionService;
            _logger = logger;
        }

        // Endpoint per ottenere tutte le azioni ERP disponibili per un client
        [HttpGet("{clientId}")]
        public async Task<IActionResult> GetAllActions(string clientId, [FromQuery] string module = null)
        {
            try
            {
                var actions = await _actionService.GetActionsAsync(clientId, module);
                return Ok(actions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nel recupero delle azioni ERP");
                return StatusCode(500, new { Success = false, Error = ex.Message });
            }
        }

        // Endpoint per ottenere una specifica azione ERP
        [HttpGet("{clientId}/{actionId}")]
        public async Task<IActionResult> GetAction(string clientId, string actionId)
        {
            try
            {
                var action = await _actionService.GetActionAsync(clientId, actionId);
                if (action == null)
                {
                    return NotFound(new { Success = false, Error = "Azione non trovata" });
                }
                return Ok(action);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nel recupero dell'azione ERP");
                return StatusCode(500, new { Success = false, Error = ex.Message });
            }
        }

        // Endpoint per registrare una nuova azione ERP
        [HttpPost]
        public async Task<IActionResult> CreateAction([FromBody] ErpActionDefinition action)
        {
            try
            {
                if (action == null)
                {
                    return BadRequest("Azione non valida");
                }

                if (string.IsNullOrEmpty(action.ClientId) || string.IsNullOrEmpty(action.Name) ||
                    string.IsNullOrEmpty(action.Description) || string.IsNullOrEmpty(action.FunctionName))
                {
                    return BadRequest("ClientId, Nome, Descrizione e FunctionName sono obbligatori");
                }

                _logger.LogInformation("Creazione nuova azione ERP per client {ClientId}: {Name}", action.ClientId, action.Name);

                var result = await _actionService.CreateActionAsync(action);
                return Ok(new { Success = true, ActionId = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nella creazione dell'azione ERP");
                return StatusCode(500, new { Success = false, Error = ex.Message });
            }
        }

        // Endpoint per aggiornare un'azione ERP esistente
        [HttpPut("{actionId}")]
        public async Task<IActionResult> UpdateAction(string actionId, [FromBody] ErpActionDefinition action)
        {
            try
            {
                if (action == null)
                {
                    return BadRequest("Azione non valida");
                }

                if (string.IsNullOrEmpty(action.ClientId) || string.IsNullOrEmpty(action.Name) ||
                    string.IsNullOrEmpty(action.Description) || string.IsNullOrEmpty(action.FunctionName))
                {
                    return BadRequest("ClientId, Nome, Descrizione e FunctionName sono obbligatori");
                }

                _logger.LogInformation("Aggiornamento azione ERP {ActionId}", actionId);

                var result = await _actionService.UpdateActionAsync(actionId, action);
                if (!result)
                {
                    return NotFound(new { Success = false, Error = "Azione non trovata" });
                }

                return Ok(new { Success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nell'aggiornamento dell'azione ERP");
                return StatusCode(500, new { Success = false, Error = ex.Message });
            }
        }

        // Endpoint per eliminare un'azione ERP
        [HttpDelete("{clientId}/{actionId}")]
        public async Task<IActionResult> DeleteAction(string clientId, string actionId)
        {
            try
            {
                _logger.LogInformation("Eliminazione azione ERP {ActionId} per client {ClientId}", actionId, clientId);

                var result = await _actionService.DeleteActionAsync(clientId, actionId);
                if (!result)
                {
                    return NotFound(new { Success = false, Error = "Azione non trovata" });
                }

                return Ok(new { Success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nell'eliminazione dell'azione ERP");
                return StatusCode(500, new { Success = false, Error = ex.Message });
            }
        }

        // Endpoint per cercare azioni ERP
        [HttpGet("search/{clientId}")]
        public async Task<IActionResult> SearchActions(string clientId, [FromQuery] string query, [FromQuery] string module = null)
        {
            try
            {
                if (string.IsNullOrEmpty(query))
                {
                    return BadRequest("Query di ricerca obbligatoria");
                }

                _logger.LogInformation("Ricerca azioni ERP per client {ClientId}: {Query}", clientId, query);

                var results = await _actionService.SearchActionsAsync(clientId, query, module);
                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nella ricerca delle azioni ERP");
                return StatusCode(500, new { Success = false, Error = ex.Message });
            }
        }

        // Endpoint per testare un'azione ERP
        [HttpPost("test")]
        public async Task<IActionResult> TestAction([FromBody] ErpActionRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Action) || string.IsNullOrEmpty(request.ClientId))
                {
                    return BadRequest("Action e ClientId sono obbligatori");
                }

                _logger.LogInformation("Test azione ERP per client {ClientId}: {Action}", request.ClientId, request.Action);

                var result = await _actionService.TestActionAsync(request.Action, request.ClientId, request.Parameters);
                return Ok(new { Success = true, Result = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nel test dell'azione ERP");
                return StatusCode(500, new { Success = false, Error = ex.Message });
            }
        }
    }
}