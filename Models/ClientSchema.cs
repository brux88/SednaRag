namespace SednaRag.Models
{
    public class ClientSchema
    {
        public string ClientId { get; set; }
        public List<TableDefinition> Tables { get; set; }
        public List<StoredProcedureDefinition> StoredProcedures { get; set; }
        public List<BusinessRuleDefinition> BusinessRules { get; set; }
        public List<QueryExample> QueryExamples { get; set; }
        public Dictionary<string, object> AdditionalMetadata { get; set; }
    }
}
