using System.Text.Json.Serialization;

namespace SednaRag.Models
{
    public class RagQueryRequest
    {
        [JsonPropertyName("query")]
        public string Query { get; set; }

        [JsonPropertyName("clientId")]
        public string ClientId { get; set; }

        [JsonPropertyName("module")]
        public string? Module { get; set; }

        [JsonPropertyName("tokenLimit")]
        public int TokenLimit { get; set; }  // Limite massimo di token per il cliente

        [JsonPropertyName("tokensUsed")]
        public int TokensUsed { get; set; }  //
    }
}
