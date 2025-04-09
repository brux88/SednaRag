namespace SednaRag.Models
{
    public class GeneratedQuery
    {
        public string SqlQuery { get; set; }
        public string Explanation { get; set; }
        public int InputTokens { get; set; }     // Cambiato da PromptTokens
        public int OutputTokens { get; set; }    // Cambiato da CompletionTokens
        public int TotalTokens { get; set; }     // Aggiunto
        public bool IsSafe { get; set; }     // Aggiunto
    }
}
