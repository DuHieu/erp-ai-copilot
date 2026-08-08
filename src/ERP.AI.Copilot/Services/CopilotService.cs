using System.Diagnostics;
using System.Text.Json;
using ERP.AI.Copilot.Prompts;
using ERP.AI.Core.Dtos;
using ERP.AI.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace ERP.AI.Copilot.Services;

public class CopilotService : ICopilotService
{
    private readonly ILlmProvider _primaryLlmProvider;
    private readonly ILlmProvider _fallbackLlmProvider;
    private readonly IErpToolRegistry _toolRegistry;
    private readonly ILogger<CopilotService> _logger;

    public CopilotService(
        ILlmProvider llmProvider,
        IErpToolRegistry toolRegistry,
        ILogger<CopilotService> logger)
    {
        _primaryLlmProvider = llmProvider;
        _fallbackLlmProvider = new Providers.FakeLlmProvider();
        _toolRegistry = toolRegistry;
        _logger = logger;
    }

    public async Task<CopilotChatResponse> ProcessMessageAsync(CopilotChatRequest request, CancellationToken cancellationToken = default)
    {
        var totalStopwatch = Stopwatch.StartNew();
        var traceId = Guid.NewGuid().ToString("N");
        var toolsUsed = new List<string>();
        var traceDetails = new List<ToolTraceDto>();
        object? structuredDataResult = null;

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return new CopilotChatResponse
            {
                Answer = "Please provide a valid question or query.",
                TraceId = traceId
            };
        }

        // 1. Guard check for direct write requests
        if (IsWriteRequest(request.Message))
        {
            return new CopilotChatResponse
            {
                Answer = "Phase 1 currently supports read-only ERP queries. Creating, modifying, updating, or deleting ERP transactions is strictly not supported.",
                TraceId = traceId,
                TotalDurationMs = totalStopwatch.ElapsedMilliseconds
            };
        }

        // 2. Prepare LLM conversation prompt
        var systemPrompt = SystemPromptManager.GetSystemPrompt();
        var availableTools = _toolRegistry.GetAllTools().Select(t => new LlmToolDefinition
        {
            Name = t.Name,
            Description = t.Description,
            ParameterJsonSchema = t.ParameterJsonSchema
        }).ToList();

        var messages = new List<LlmChatMessage>
        {
            new() { Role = "system", Content = systemPrompt },
            new() { Role = "user", Content = request.Message }
        };

        var llmRequest = new LlmChatRequest
        {
            Messages = messages,
            AvailableTools = availableTools
        };

        LlmChatResponse llmResponse;
        try
        {
            llmResponse = await _primaryLlmProvider.ChatAsync(llmRequest, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Primary LLM provider failed. Falling back to offline engine.");
            llmResponse = await _fallbackLlmProvider.ChatAsync(llmRequest, cancellationToken);
        }

        // 3. Process Tool Calls if requested by LLM
        if (llmResponse.HasToolCalls)
        {
            foreach (var toolCall in llmResponse.ToolCalls)
            {
                toolsUsed.Add(toolCall.Name);

                var toolStopwatch = Stopwatch.StartNew();
                var (success, result, error) = await _toolRegistry.ExecuteToolAsync(toolCall.Name, toolCall.ArgumentsJson, cancellationToken);
                toolStopwatch.Stop();

                traceDetails.Add(new ToolTraceDto
                {
                    ToolName = toolCall.Name,
                    ExecutionDurationMs = toolStopwatch.ElapsedMilliseconds,
                    Success = success,
                    ErrorMessage = error
                });

                if (success && result != null)
                {
                    structuredDataResult = result;
                    var resultJson = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = false });

                    // Append tool execution turn for synthesis
                    messages.Add(new LlmChatMessage
                    {
                        Role = "assistant",
                        Content = $"Executing ERP Tool '{toolCall.Name}' with parameters: {toolCall.ArgumentsJson}"
                    });

                    messages.Add(new LlmChatMessage
                    {
                        Role = "tool",
                        ToolCallId = toolCall.Id,
                        Content = resultJson
                    });

                    // Second turn: synthesis of natural language answer
                    try
                    {
                        var synthesisResponse = await _primaryLlmProvider.ChatAsync(new LlmChatRequest
                        {
                            Messages = messages,
                            AvailableTools = availableTools
                        }, cancellationToken);

                        if (!string.IsNullOrWhiteSpace(synthesisResponse.Content))
                        {
                            llmResponse.Content = synthesisResponse.Content;
                        }
                    }
                    catch
                    {
                        var fallbackSynth = await _fallbackLlmProvider.ChatAsync(new LlmChatRequest
                        {
                            Messages = messages,
                            AvailableTools = availableTools
                        }, cancellationToken);
                        llmResponse.Content = fallbackSynth.Content;
                    }
                }
                else
                {
                    llmResponse.Content = $"Unable to complete ERP query: {error}";
                }
            }
        }

        totalStopwatch.Stop();

        return new CopilotChatResponse
        {
            Answer = FormatCleanAnswer(llmResponse.Content),
            ToolsUsed = toolsUsed.Distinct().ToList(),
            Data = structuredDataResult,
            TraceId = traceId,
            TraceDetails = traceDetails,
            TotalDurationMs = totalStopwatch.ElapsedMilliseconds
        };
    }

    private static bool IsWriteRequest(string message)
    {
        var m = message.ToLowerInvariant();
        return m.StartsWith("tạo") || m.StartsWith("xóa") || m.StartsWith("sửa") ||
               m.StartsWith("create") || m.StartsWith("delete") || m.StartsWith("update") ||
               m.Contains("thanh toán mới") || m.Contains("payment voucher");
    }

    private static string FormatCleanAnswer(string rawContent)
    {
        if (string.IsNullOrWhiteSpace(rawContent))
        {
            return "No information was returned for this query.";
        }

        // Clean out internal code block artifacts if present
        return rawContent.Trim();
    }
}
