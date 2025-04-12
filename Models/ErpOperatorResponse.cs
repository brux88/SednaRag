using System.Text.Json.Serialization;

namespace SednaRag.Models
{
    public class ErpOperatorResponse
    {
        public bool Success { get; set; }
        public string ResponseText { get; set; }
        public string ActionPerformed { get; set; }
        public object ActionResult { get; set; }
        public int TokensUsed { get; set; }
        public int TokensRemaining { get; set; }
        [JsonPropertyName("routerTokens")]
        public int RouterTokens { get; set; } // Nuovo campo
    }
}
