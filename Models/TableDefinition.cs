using System.Text.Json.Serialization;

namespace SednaRag.Models
{
    public class TableDefinition
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("columns")]
        public List<ColumnDefinition> Columns { get; set; }

        [JsonPropertyName("relations")]
        public List<RelationDefinition> Relations { get; set; }

        [JsonPropertyName("indexes")]
        public List<IndexDefinition> Indexes { get; set; }

        [JsonPropertyName("commonUsage")]
        public string CommonUsage { get; set; }

        [JsonPropertyName("module")]
        public string Module { get; set; }

        [JsonPropertyName("keywords")]
        public List<string> Keywords { get; set; }
    }
}
