using System.Text.Json.Serialization;

namespace SednaRag.Models
{
    public class SchemaImportRequest
    {
        [JsonPropertyName("clientId")]
        public string ClientId { get; set; }

        [JsonPropertyName("tables")]
        public List<TableDefinition> Tables { get; set; }

        [JsonPropertyName("storedProcedures")]
        public List<StoredProcedureDefinition> StoredProcedures { get; set; }

        [JsonPropertyName("businessRules")]
        public List<BusinessRuleDefinition> BusinessRules { get; set; }

        [JsonPropertyName("queryExamples")]
        public List<QueryExample> QueryExamples { get; set; }

        [JsonPropertyName("additionalMetadata")]
        public Dictionary<string, object> AdditionalMetadata { get; set; }
    }

}
