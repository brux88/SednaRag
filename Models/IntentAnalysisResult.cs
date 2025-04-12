namespace SednaRag.Models
{
    public class IntentAnalysisResult
    {
        public AgentType AgentType { get; set; }
        public float Confidence { get; set; }
        public string Explanation { get; set; }
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
    }
}
