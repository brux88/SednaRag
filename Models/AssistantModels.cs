using System.Text.Json.Serialization;

namespace SednaRag.Models
{
    public class AssistantQueryRequest
    {
        [JsonPropertyName("query")]
        public string Query { get; set; }

        [JsonPropertyName("clientId")]
        public string ClientId { get; set; }

        [JsonPropertyName("module")]
        public string? Module { get; set; }

        [JsonPropertyName("context")]
        public AssistantContext? Context { get; set; }
    }

    public class AssistantContext
    {
        [JsonPropertyName("conversationId")]
        public string ConversationId { get; set; }

        [JsonPropertyName("history")]
        public List<AssistantMessage> History { get; set; }

        [JsonPropertyName("userInfo")]
        public UserInfo UserInfo { get; set; }

        [JsonPropertyName("currentScreen")]
        public string CurrentScreen { get; set; }

        [JsonPropertyName("selectedData")]
        public Dictionary<string, object> SelectedData { get; set; }
    }

    public class AssistantMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } // "user" or "assistant"

        [JsonPropertyName("content")]
        public string Content { get; set; }

        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }
    }

    public class UserInfo
    {
        [JsonPropertyName("userId")]
        public string UserId { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("role")]
        public string Role { get; set; }

        [JsonPropertyName("permissions")]
        public List<string> Permissions { get; set; }
    }

    public class AssistantResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("response")]
        public string Response { get; set; }

        [JsonPropertyName("agentUsed")]
        public string AgentUsed { get; set; }

        [JsonPropertyName("suggestedActions")]
        public List<SuggestedAction> SuggestedActions { get; set; }

        [JsonPropertyName("tokensUsed")]
        public TokenUsage TokensUsed { get; set; }

        [JsonPropertyName("tokensRemaining")]
        public int TokensRemaining { get; set; }

        [JsonPropertyName("error")]
        public string Error { get; set; }
    }

    public class SuggestedAction
    {
        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("actionType")]
        public string ActionType { get; set; } // "rag", "support", "erp-action"

        [JsonPropertyName("actionId")]
        public string ActionId { get; set; }

        [JsonPropertyName("parameters")]
        public Dictionary<string, object> Parameters { get; set; }
    }

    public class SupportQueryRequest
    {
        [JsonPropertyName("query")]
        public string Query { get; set; }

        [JsonPropertyName("clientId")]
        public string ClientId { get; set; }

        [JsonPropertyName("module")]
        public string Module { get; set; }
    }

    public class ErpActionRequest
    {
        [JsonPropertyName("action")]
        public string Action { get; set; }

        [JsonPropertyName("clientId")]
        public string ClientId { get; set; }

        [JsonPropertyName("parameters")]
        public Dictionary<string, object> Parameters { get; set; }
    }

    public class SupportQueryResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("response")]
        public string Response { get; set; }

        [JsonPropertyName("relevantDocuments")]
        public List<SupportDocumentReference> RelevantDocuments { get; set; }

        [JsonPropertyName("tokensUsed")]
        public TokenUsage TokensUsed { get; set; }

        [JsonPropertyName("tokensRemaining")]
        public int TokensRemaining { get; set; }

        [JsonPropertyName("error")]
        public string Error { get; set; }
    }

    public class SupportDocumentReference
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("snippet")]
        public string Snippet { get; set; }

        [JsonPropertyName("documentType")]
        public string DocumentType { get; set; }
    }

    public class ErpActionResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("result")]
        public object Result { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; }

        [JsonPropertyName("error")]
        public string Error { get; set; }
    }
}