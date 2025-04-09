using System.Text.Json.Serialization;

namespace SednaRag.Models
{
    public class GeneratedQueryJson
    {
        [JsonPropertyName("sql_query")]
        public string SqlQuery { get; set; }

        [JsonPropertyName("explanation")]
        public string Explanation { get; set; }
    }
}
