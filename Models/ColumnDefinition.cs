using System.Text.Json.Serialization;

namespace SednaRag.Models
{
    public class ColumnDefinition
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("dataType")]
        public string DataType { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("isPrimaryKey")]
        public bool IsPrimaryKey { get; set; }

        [JsonPropertyName("isForeignKey")]
        public bool IsForeignKey { get; set; }

        [JsonPropertyName("referencedTable")]
        public string? ReferencedTable { get; set; }

        [JsonPropertyName("referencedColumn")]
        public string? ReferencedColumn { get; set; }
    }
}
