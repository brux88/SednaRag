using System.Text.Json.Serialization;

namespace SednaRag.Models
{
    public class StoredProcedureDefinition
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("parameters")]
        public List<ParameterDefinition> Parameters { get; set; }

        [JsonPropertyName("resultDescription")]
        public string ResultDescription { get; set; }

        [JsonPropertyName("usage")]
        public string Usage { get; set; }

        [JsonPropertyName("module")]
        public string Module { get; set; }

        [JsonPropertyName("keywords")]
        public List<string> Keywords { get; set; }
    }
}
