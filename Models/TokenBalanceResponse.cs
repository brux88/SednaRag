namespace SednaRag.Models
{
    public class TokenBalanceResponse
    {
        public string ApiKey { get; set; }
        public Guid ClienteId { get; set; }
        public string RagioneSociale { get; set; }
        public int TokensRemaining { get; set; }
        public DateTime LastUpdated { get; set; }
        public int TransactionCount { get; set; }
        public int LastWeekUsage { get; set; }
    }
}
