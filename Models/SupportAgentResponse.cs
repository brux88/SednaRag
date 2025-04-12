using System.Text.Json.Serialization;

namespace SednaRag.Models
{
    public class SupportAgentResponse
    {
        public bool Success { get; set; }
        public string ResponseText { get; set; }
        public List<string> RelatedTopics { get; set; }
        public int TokensUsed { get; set; }
        public int TokensRemaining { get; set; }
        [JsonPropertyName("routerTokens")]
        public int RouterTokens { get; set; } // Nuovo campo
    }
}
