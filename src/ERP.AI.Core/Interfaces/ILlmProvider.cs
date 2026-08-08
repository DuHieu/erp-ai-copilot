namespace ERP.AI.Core.Interfaces;

public class LlmChatMessage
{
    public string Role { get; set; } = "user"; // "system", "user", "assistant", "tool"
    public string Content { get; set; } = string.Empty;
    public string? ToolCallId { get; set; }
    public List<LlmToolCall>? ToolCalls { get; set; }
}

public class LlmToolDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ParameterJsonSchema { get; set; } = "{}";
}

public class LlmToolCall
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string ArgumentsJson { get; set; } = "{}";
}

public class LlmChatRequest
{
    public List<LlmChatMessage> Messages { get; set; } = new();
    public List<LlmToolDefinition> AvailableTools { get; set; } = new();
    public double Temperature { get; set; } = 0.1;
}

public class LlmChatResponse
{
    public string Content { get; set; } = string.Empty;
    public List<LlmToolCall> ToolCalls { get; set; } = new();
    public bool HasToolCalls => ToolCalls != null && ToolCalls.Count > 0;
}

public interface ILlmProvider
{
    Task<LlmChatResponse> ChatAsync(LlmChatRequest request, CancellationToken cancellationToken = default);
}
