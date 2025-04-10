using System.ComponentModel.DataAnnotations;

namespace SednaRag.Models
{
    public class TokenUsageRequest
    {
        [Required]
        public string ApiKey { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int TokensUsed { get; set; }

        public string Module { get; set; }

        public string PromptType { get; set; }

        public string QueryText { get; set; }

        public string ReferenceId { get; set; }
    }
}
