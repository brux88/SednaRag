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
 
    }
}
