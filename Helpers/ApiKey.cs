namespace SednaRag.Helpers
{
    // ApiKeyAttribute.cs
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Filters;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using SednaRag.Models;
    using System;
    using System.Security.Cryptography;
    using System.Threading.Tasks;
    using static System.Runtime.InteropServices.JavaScript.JSType;

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class ApiKeyAttribute : Attribute, IAsyncActionFilter
    {
        private const string ApiKeyHeaderName = "X-API-Key";
        private readonly HttpClient _httpClient;
        private readonly string _licenseServiceBaseUrl;

        public ApiKeyAttribute()
        {
            _httpClient = new HttpClient();
            // Idealmente, questa URL dovrebbe venire dalla configurazione
            _licenseServiceBaseUrl = "https://sednalicenseapp.azurewebsites.net/api/clienti";
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // Verifica se l'header API Key è presente
            if (!context.HttpContext.Request.Headers.TryGetValue(ApiKeyHeaderName, out var apiKey))
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            try
            {
                if (string.IsNullOrEmpty(apiKey))
                {
                    context.Result = new ObjectResult(new
                    {
                        message = "Api Key Vuota."
                    })
                    {
                        StatusCode = 403 // Forbidden
                    };
                    return;
                }
                // Utilizza il nuovo endpoint getTokenBalance per ottenere il saldo reale
                _httpClient.DefaultRequestHeaders.Add("ApiKey", $"{apiKey}");
                var response = await _httpClient.GetAsync($"{_licenseServiceBaseUrl}/getTokenBalance/{apiKey}");

                if (!response.IsSuccessStatusCode)
                {
                    context.Result = new UnauthorizedResult();
                    return;
                }

                // Deserializza la risposta completa
                var tokenBalance = await response.Content.ReadFromJsonAsync<TokenBalanceResponse>();

                if (tokenBalance.TokensRemaining <= 0)
                {
                    context.Result = new ObjectResult(new
                    {
                        message = "Limite di richieste AI raggiunto. Acquista ulteriori crediti.",
                        tokensRemaining = tokenBalance.TokensRemaining
                    })
                    {
                        StatusCode = 403 // Forbidden
                    };
                    return;
                }

                // Salva tutte le informazioni utili nel contesto HTTP
                context.HttpContext.Items["ApiKey"] = apiKey.ToString();
                context.HttpContext.Items["RichiesteDisponibili"] = tokenBalance.TokensRemaining;
                context.HttpContext.Items["ClienteId"] = tokenBalance.ClienteId;
                context.HttpContext.Items["RagioneSociale"] = tokenBalance.RagioneSociale;
                context.HttpContext.Items["LastWeekUsage"] = tokenBalance.LastWeekUsage;


                // Procedi con l'esecuzione
                await next();
            }
            catch (Exception ex)
            {
                // Log dell'errore
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILogger<ApiKeyAttribute>>();
                logger.LogError(ex, "Errore durante la validazione dell'API key");

                context.Result = new StatusCodeResult(500);
                return;
            }
        }
    }
}