using System.Text.Json.Serialization;

namespace SednaRag.Models
{
    public class BusinessRuleDefinition
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("details")]
        public string Details { get; set; }

        [JsonPropertyName("relatedTables")]
        public List<string> RelatedTables { get; set; }

        [JsonPropertyName("examples")]
        public string Examples { get; set; }

        [JsonPropertyName("module")]
        public string Module { get; set; }

        [JsonPropertyName("keywords")]
        public List<string> Keywords { get; set; }
    }

}
