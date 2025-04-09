using System.Text.Json.Serialization;

namespace SednaRag.Models
{
    public class ParameterDefinition
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("dataType")]
        public string DataType { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("isOutput")]
        public bool IsOutput { get; set; }
    }
}
