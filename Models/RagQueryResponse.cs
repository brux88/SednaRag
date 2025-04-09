using System.Text.Json.Serialization;

namespace SednaRag.Models
{
    public class RagQueryResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("queryGenerated")]
        public string QueryGenerated { get; set; }

        [JsonPropertyName("explanation")]
        public string Explanation { get; set; }

        [JsonPropertyName("relevantContexts")]
        public List<RelevantContext> RelevantContexts { get; set; }

        [JsonPropertyName("tokensUsed")]
        public TokenUsage TokensUsed { get; set; }

        [JsonPropertyName("tokenLimit")]
        public int TokenLimit { get; set; }

        [JsonPropertyName("totalTokensUsed")]
        public int TotalTokensUsed { get; set; }

        [JsonPropertyName("tokensRemaining")]
        public int TokensRemaining { get; set; }

        [JsonPropertyName("error")]
        public string Error { get; set; }
    }

}
