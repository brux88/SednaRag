using System;
using System.ComponentModel.DataAnnotations;

namespace SednaRag.Models
{
    public enum AgentType
    {
        Unknown,
        RagSqlGenerator,
        SupportAgent,
        ErpOperator
    }
}
