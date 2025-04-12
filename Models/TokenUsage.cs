using System.Text.Json.Serialization;

namespace SednaRag.Models
{

    public class TokenUsage
    {
        [JsonPropertyName("inputTokens")]
        public int InputTokens { get; set; }

        [JsonPropertyName("outputTokens")]
        public int OutputTokens { get; set; }

        [JsonPropertyName("totalTokens")]
        public int TotalTokens { get; set; }

        [JsonPropertyName("embeddingTokens")]
        public int EmbeddingTokens { get; set; }

        [JsonPropertyName("totalInQuery")]
        public int TotalInQuery { get; set; }

        [JsonPropertyName("routerTokens")]
        public int RouterTokens { get; set; } // Nuovo campo
    }
}
