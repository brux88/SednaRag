namespace SednaRag.Models
{
    public class AgentResponse
    {
        public bool Success { get; set; }
        public string ResponseText { get; set; }
        public string GeneratedSql { get; set; }
        public string ActionPerformed { get; set; }
        public object ActionResult { get; set; }
        public AgentType AgentType { get; set; }
        public int TokensUsed { get; set; }
        public int TokensRemaining { get; set; }
        public string Error { get; set; }
    }
}
