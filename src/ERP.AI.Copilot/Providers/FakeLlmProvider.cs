using ERP.AI.Core.Interfaces;

namespace ERP.AI.Copilot.Providers;

public class FakeLlmProvider : ILlmProvider
{
    public Task<LlmChatResponse> ChatAsync(LlmChatRequest request, CancellationToken cancellationToken = default)
    {
        var lastUserMessage = request.Messages.LastOrDefault(m => m.Role == "user")?.Content ?? string.Empty;
        var lastToolMessage = request.Messages.LastOrDefault(m => m.Role == "tool")?.Content;

        // Step 2: If previous turn executed a tool and returned result, summarize it
        if (!string.IsNullOrWhiteSpace(lastToolMessage))
        {
            var summary = SynthesizeToolResult(lastUserMessage, lastToolMessage);
            return Task.FromResult(new LlmChatResponse { Content = summary });
        }

        // Step 1: Pattern match user question to tool selection
        var toolCall = MatchQuestionToTool(lastUserMessage);
        if (toolCall != null)
        {
            return Task.FromResult(new LlmChatResponse
            {
                Content = $"Selected tool: {toolCall.Name}",
                ToolCalls = new List<LlmToolCall> { toolCall }
            });
        }

        // Write operation refusal
        if (IsWriteRequest(lastUserMessage))
        {
            return Task.FromResult(new LlmChatResponse
            {
                Content = "Phase 1 currently supports read-only ERP queries. Creating, updating, or modifying ERP transactions is not supported."
            });
        }

        // Default natural answer
        return Task.FromResult(new LlmChatResponse
        {
            Content = "I am ERP AI Copilot. Please ask questions about receivables (top debtors), customer balances, revenue summaries, low stock inventory, or project budget alerts."
        });
    }

    private static LlmToolCall? MatchQuestionToTool(string question)
    {
        var q = question.ToLowerInvariant();

        if (q.Contains("top") || q.Contains("nợ nhiều nhất") || q.Contains("debts") || q.Contains("debtors"))
        {
            return new LlmToolCall
            {
                Name = "GetTopDebtors",
                ArgumentsJson = """{"top": 5}"""
            };
        }

        if (q.Contains("maeda") || (q.Contains("khách hàng") && (q.Contains("chi tiết") || q.Contains("nợ bao nhiêu"))))
        {
            return new LlmToolCall
            {
                Name = "GetCustomerReceivable",
                ArgumentsJson = """{"customerCode": "CUS001"}"""
            };
        }

        if (q.Contains("doanh thu") || q.Contains("revenue") || q.Contains("tháng 7"))
        {
            return new LlmToolCall
            {
                Name = "GetRevenueSummary",
                ArgumentsJson = """{"from": "2026-07-01", "to": "2026-07-31"}"""
            };
        }

        if (q.Contains("hết hàng") || q.Contains("tồn kho") || q.Contains("inventory") || q.Contains("stock"))
        {
            return new LlmToolCall
            {
                Name = "GetInventoryAlerts",
                ArgumentsJson = "{}"
            };
        }

        if (q.Contains("vượt ngân sách") || q.Contains("budget") || q.Contains("vượt budget") || q.Contains("project"))
        {
            return new LlmToolCall
            {
                Name = "GetProjectBudgetAlerts",
                ArgumentsJson = "{}"
            };
        }

        return null;
    }

    private static bool IsWriteRequest(string text)
    {
        var t = text.ToLowerInvariant();
        return t.Contains("tạo") || t.Contains("xóa") || t.Contains("sửa") ||
               t.Contains("create") || t.Contains("delete") || t.Contains("drop") ||
               t.Contains("update") || t.Contains("insert") || t.Contains("payment voucher");
    }

    private static string SynthesizeToolResult(string userQuestion, string toolResultJson)
    {
        return $"Based on the ERP backend data, here is the answer for '{userQuestion}':\n\n{toolResultJson}";
    }
}
