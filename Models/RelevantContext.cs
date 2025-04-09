using System.Text.Json.Serialization;

namespace SednaRag.Models
{
    public class RelevantContext
    {
        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("contextType")]
        public string ContextType { get; set; }

        [JsonPropertyName("snippet")]
        public string Snippet { get; set; }
    }
}
