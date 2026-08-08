using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ERP.AI.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ERP.AI.Copilot.Providers;

public class OllamaLlmProvider : ILlmProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaLlmProvider> _logger;
    private readonly string _model;
    private readonly string _endpoint;

    public OllamaLlmProvider(HttpClient httpClient, IConfiguration configuration, ILogger<OllamaLlmProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _endpoint = configuration["AI:Endpoint"] ?? "http://localhost:11434";
        _model = configuration["AI:Model"] ?? "qwen3";
    }

    public async Task<LlmChatResponse> ChatAsync(LlmChatRequest request, CancellationToken cancellationToken = default)
    {
        var url = $"{_endpoint.TrimEnd('/')}/api/chat";

        var payloadMessages = new List<object>();
        foreach (var msg in request.Messages)
        {
            if (msg.Role == "tool")
            {
                payloadMessages.Add(new
                {
                    role = "tool",
                    content = msg.Content
                });
            }
            else
            {
                payloadMessages.Add(new
                {
                    role = msg.Role,
                    content = msg.Content
                });
            }
        }

        var toolsPayload = request.AvailableTools.Select(t => new
        {
            type = "function",
            function = new
            {
                name = t.Name,
                description = t.Description,
                parameters = JsonSerializer.Deserialize<JsonObject>(t.ParameterJsonSchema)
            }
        }).ToList();

        var body = new
        {
            model = _model,
            messages = payloadMessages,
            tools = toolsPayload.Count > 0 ? toolsPayload : null,
            stream = false,
            options = new
            {
                temperature = request.Temperature
            }
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(url, body, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errorText = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Ollama returned status {StatusCode}: {Error}", response.StatusCode, errorText);
                throw new HttpRequestException($"Ollama service returned {response.StatusCode}: {errorText}");
            }

            var jsonResult = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken: cancellationToken);
            if (jsonResult == null || !jsonResult.ContainsKey("message"))
            {
                return new LlmChatResponse { Content = "No response message received from Ollama." };
            }

            var messageObj = jsonResult["message"]?.AsObject();
            var content = messageObj?["content"]?.ToString() ?? string.Empty;

            var toolCalls = new List<LlmToolCall>();
            if (messageObj != null && messageObj.ContainsKey("tool_calls"))
            {
                var toolCallsArray = messageObj["tool_calls"]?.AsArray();
                if (toolCallsArray != null)
                {
                    foreach (var tc in toolCallsArray)
                    {
                        var funcObj = tc?["function"]?.AsObject();
                        if (funcObj != null)
                        {
                            var name = funcObj["name"]?.ToString() ?? string.Empty;
                            var argsObj = funcObj["arguments"];
                            var argsJson = argsObj != null ? argsObj.ToJsonString() : "{}";

                            toolCalls.Add(new LlmToolCall
                            {
                                Name = name,
                                ArgumentsJson = argsJson
                            });
                        }
                    }
                }
            }

            // Fallback: parse JSON tool call block in text if tool_calls array was empty
            if (toolCalls.Count == 0 && !string.IsNullOrWhiteSpace(content))
            {
                var fallbackCall = TryExtractJsonToolCall(content, request.AvailableTools);
                if (fallbackCall != null)
                {
                    toolCalls.Add(fallbackCall);
                }
            }

            return new LlmChatResponse
            {
                Content = content,
                ToolCalls = toolCalls
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to Ollama at {Endpoint}", _endpoint);
            throw;
        }
    }

    private static LlmToolCall? TryExtractJsonToolCall(string content, List<LlmToolDefinition> availableTools)
    {
        var validNames = availableTools.Select(t => t.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Check if message contains {"name": "...", "arguments": ...}
        int startIndex = content.IndexOf('{');
        int endIndex = content.LastIndexOf('}');

        if (startIndex >= 0 && endIndex > startIndex)
        {
            var jsonSub = content.Substring(startIndex, endIndex - startIndex + 1);
            try
            {
                using var doc = JsonDocument.Parse(jsonSub);
                var root = doc.RootElement;
                if (root.TryGetProperty("name", out var nameProp) || root.TryGetProperty("tool", out nameProp))
                {
                    string name = nameProp.GetString() ?? string.Empty;
                    if (validNames.Contains(name))
                    {
                        string argsJson = "{}";
                        if (root.TryGetProperty("arguments", out var argsProp) || root.TryGetProperty("parameters", out argsProp))
                        {
                            argsJson = argsProp.GetRawText();
                        }

                        return new LlmToolCall
                        {
                            Name = name,
                            ArgumentsJson = argsJson
                        };
                    }
                }
            }
            catch
            {
                // Not valid JSON tool call
            }
        }

        return null;
    }
}
