using System.Text.Json.Serialization;

namespace SednaRag.Models
{
    public class SchemaImportResult
    {
        [JsonPropertyName("documentsCreated")]
        public int DocumentsCreated { get; set; }

        [JsonPropertyName("documentsUpdated")]
        public int DocumentsUpdated { get; set; }
    }
}
