using System.Text.Json.Serialization;

namespace SednaRag.Models
{
    public class IndexDefinition
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("columns")]
        public List<string> Columns { get; set; }

        [JsonPropertyName("isUnique")]
        public bool IsUnique { get; set; }
    }
}
