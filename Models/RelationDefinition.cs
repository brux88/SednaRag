using System.Text.Json.Serialization;

namespace SednaRag.Models
{
    public class RelationDefinition
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } // "OneToMany", "ManyToOne", "OneToOne", "ManyToMany"

        [JsonPropertyName("fromColumn")]
        public string FromColumn { get; set; }

        [JsonPropertyName("toTable")]
        public string ToTable { get; set; }

        [JsonPropertyName("toColumn")]
        public string ToColumn { get; set; }
    }
}
