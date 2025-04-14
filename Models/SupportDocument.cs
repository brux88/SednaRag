using System.Text.Json.Serialization;

namespace SednaRag.Models
{
    public class SupportDocument
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("clientId")]
        public string ClientId { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("content")]
        public string Content { get; set; }

        [JsonPropertyName("contentVector")]
        public float[]? ContentVector { get; set; }

        [JsonPropertyName("module")]
        public string? Module { get; set; }

        [JsonPropertyName("tags")]
        public List<string>? Tags { get; set; }

        [JsonPropertyName("documentType")]
        public string? DocumentType { get; set; } // FAQ, Guide, Tutorial, etc.

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("updatedAt")]
        public DateTime UpdatedAt { get; set; }
    }
}