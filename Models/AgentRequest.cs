namespace SednaRag.Models
{
    public class AgentRequest
    {
        public string Query { get; set; }
        public string ClientId { get; set; }
        public string? Module { get; set; }
        public Dictionary<string, object>? AdditionalParameters { get; set; }
    }
}
