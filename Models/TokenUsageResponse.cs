using System;
using System.ComponentModel.DataAnnotations;

namespace SednaRag.Models
{
    public class TokenUsageResponse
    {
        public int TokensRemaining { get; set; }
        public DateTime? LastUpdated { get; set; }
        public Guid TransactionId { get; set; }
    }
}
