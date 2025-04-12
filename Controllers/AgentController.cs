using Microsoft.AspNetCore.Mvc;
using SednaRag.Helpers;
using SednaRag.Models;
using SednaRag.Services;
using System.Text.Json;

namespace SednaRag.Controllers
{
    [ApiKey]
    [ApiController]
    [Route("api/[controller]")]
    public class AgentController : ControllerBase
    {
        private readonly SqlOperatorService _ragService;
        private readonly SupportAgentService _supportService;
        private readonly ErpOperatorService _erpService;
        private readonly AgentOrchestratorService _orchestratorService;
        private readonly ILogger<AgentController> _logger;

        public AgentController(
            SqlOperatorService ragService,
            SupportAgentService supportService,
            ErpOperatorService erpService,
            AgentOrchestratorService orchestratorService,
            ILogger<AgentController> logger)
        {
            _ragService = ragService;
            _supportService = supportService;
            _erpService = erpService;
            _orchestratorService = orchestratorService;
            _logger = logger;
        }

        // Endpoint per l'assistente multi-agente
        [HttpPost("query")]
        public async Task<IActionResult> QueryAssistant([FromBody] AssistantQueryRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Query) || string.IsNullOrEmpty(request.ClientId))
                {
                    return BadRequest("Query e ClientId sono obbligatori");
                }

                _logger.LogInformation("Nuova richiesta all'assistente: {Query}", request.Query);

                // Delega all'orchestratore che deciderà quale agente utilizzare
                var result = await _orchestratorService.ProcessQueryAsync(
                    request.Query,
                    request.ClientId,
                    request.Module,
                    request.Context);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nell'elaborazione della richiesta all'assistente");
                return StatusCode(500, new { Success = false, Error = ex.Message });
            }
        }

        // Endpoint per interrogare direttamente il RAG SQL Generator
        [HttpPost("rag")]
        public async Task<IActionResult> QueryRag([FromBody] RagQueryRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Query) || string.IsNullOrEmpty(request.ClientId))
                {
                    return BadRequest("Query e ClientId sono obbligatori");
                }

                _logger.LogInformation("Richiesta diretta al RAG SQL Generator: {Query}", request.Query);

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
        }

        // Endpoint per interrogare direttamente il Support Agent
        [HttpPost("support")]
        public async Task<IActionResult> QuerySupport([FromBody] SupportQueryRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Query) || string.IsNullOrEmpty(request.ClientId))
                {
                    return BadRequest("Query e ClientId sono obbligatori");
                }

                _logger.LogInformation("Richiesta diretta al Support Agent: {Query}", request.Query);

                var result = await _supportService.ProcessQueryAsync(
                    request.Query,
                    request.ClientId,
                    request.Module);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nell'elaborazione query al servizio di supporto");
                return StatusCode(500, new { Success = false, Error = ex.Message });
            }
        }

        // Endpoint per eseguire operazioni ERP direttamente
        [HttpPost("erp-action")]
        public async Task<IActionResult> ExecuteErpAction([FromBody] ErpActionRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Action) || string.IsNullOrEmpty(request.ClientId))
                {
                    return BadRequest("Action e ClientId sono obbligatori");
                }

                _logger.LogInformation("Richiesta azione ERP: {Action}", request.Action);

                var result = await _erpService.ExecuteActionAsync(
                    request.Action,
                    request.ClientId,
                    request.Parameters);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nell'esecuzione dell'azione ERP");
                return StatusCode(500, new { Success = false, Error = ex.Message });
            }
        }

        // Endpoint di test per verificare che gli agenti siano disponibili
        [HttpGet("status")]
        public IActionResult CheckStatus()
        {
            return Ok(new
            {
                Status = "OK",
                Timestamp = DateTime.UtcNow,
                Version = "1.0.0",
                Agents = new[] { "RAG SQL Generator", "Support Agent", "ERP Operator" }
            });
        }
    }
}