using System.Text.Json.Serialization;

namespace SednaRag.Models
{
    public class SaveQueryExampleRequest
    {
        [JsonPropertyName("clientId")]
        public string ClientId { get; set; }

        [JsonPropertyName("query")]
        public string Query { get; set; }

        [JsonPropertyName("sqlQuery")]
        public string SqlQuery { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("explanation")]
        public string Explanation { get; set; }

        [JsonPropertyName("useCase")]
        public string UseCase { get; set; }

        [JsonPropertyName("module")]
        public string Module { get; set; }

        [JsonPropertyName("keywords")]
        public List<string> Keywords { get; set; }
    }
}
