using System.Text.Json.Serialization;

namespace SednaRag.Models
{
    public class ExecuteQueryRequest
    {
        [JsonPropertyName("sqlQuery")]
        public string SqlQuery { get; set; }

        [JsonPropertyName("clientId")]
        public string ClientId { get; set; }

        [JsonPropertyName("parameters")]
        public Dictionary<string, object> Parameters { get; set; }
    }
}
