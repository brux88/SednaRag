namespace SednaRag.Helpers
{
    // ApiKeyAttribute.cs
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Filters;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using SednaRag.Models;
    using System;
    using System.Net.Http;
    using System.Security.Cryptography;
    using System.Threading.Tasks;
    using static System.Runtime.InteropServices.JavaScript.JSType;

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class ApiKeyAdminAttribute : Attribute, IAsyncActionFilter
    {
        private const string ApiKeyHeaderName = "X-API-Key";
        private readonly HttpClient _httpClient;
 

        public ApiKeyAdminAttribute()
        {
            _httpClient = new HttpClient();
            // Idealmente, questa URL dovrebbe venire dalla configurazione
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
                if (apiKey != "F8DFEB30-FE53-41A7-95C2-1BA9F7EF99B3")
                {
                    context.Result = new UnauthorizedResult();
                    return;
                }

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