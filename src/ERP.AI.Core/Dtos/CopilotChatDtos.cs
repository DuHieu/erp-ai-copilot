using System.Text.Json.Serialization;

namespace ERP.AI.Core.Dtos;

public class CopilotChatRequest
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("conversationId")]
    public string? ConversationId { get; set; }
}

public class ToolTraceDto
{
    [JsonPropertyName("toolName")]
    public string ToolName { get; set; } = string.Empty;

    [JsonPropertyName("executionDurationMs")]
    public long ExecutionDurationMs { get; set; }

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }
}

public class CopilotChatResponse
{
    [JsonPropertyName("answer")]
    public string Answer { get; set; } = string.Empty;

    [JsonPropertyName("toolsUsed")]
    public List<string> ToolsUsed { get; set; } = new();

    [JsonPropertyName("data")]
    public object? Data { get; set; }

    [JsonPropertyName("traceId")]
    public string TraceId { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("traceDetails")]
    public List<ToolTraceDto> TraceDetails { get; set; } = new();

    [JsonPropertyName("totalDurationMs")]
    public long TotalDurationMs { get; set; }
}
