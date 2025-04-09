namespace SednaRag.Middleware
{
    public class ClientContextMiddleware
    {
        private readonly RequestDelegate _next;

        public ClientContextMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Estrai client ID da header o claim
            string clientId = context.Request.Headers["X-Client-Id"];

            if (string.IsNullOrEmpty(clientId) && context.User.Identity.IsAuthenticated)
            {
                // Fallback su claim se presente
                clientId = context.User.FindFirst("ClientId")?.Value;
            }

            if (!string.IsNullOrEmpty(clientId))
            {
                // Aggiungi info cliente agli items del context per uso nei controller
                context.Items["ClientId"] = clientId;
            }

            await _next(context);
        }
    }
}
