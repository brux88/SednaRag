using System.Text.Json.Serialization;

namespace SednaRag.Models
{
    public class SchemaDocument
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("clientId")]
        public string ClientId { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("content")]
        public string Content { get; set; }

        [JsonPropertyName("contentVector")]
        public float[] ContentVector { get; set; }

        [JsonPropertyName("contentType")]
        public string ContentType { get; set; }

        [JsonPropertyName("module")]
        public string Module { get; set; }

        [JsonPropertyName("keywords")]
        public List<string> Keywords { get; set; }
    }
}
