using System.Collections.Concurrent;

namespace ERP.AI.Knowledge.Services;

/// <summary>
/// Simple in-memory conversation store for RAG chat sessions.
/// Keeps recent turn history keyed by ConversationId (GUID string).
/// Each turn stores the user question and the assistant's grounded answer.
/// Conversation history is used only for follow-up question disambiguation —
/// it is NOT treated as authoritative evidence for new answers.
/// </summary>
public sealed class KnowledgeRagConversationStore
{
    private const int CleanupIntervalMinutes = 15;
    private const int EvictionIdleMinutes = 30;

    private readonly ConcurrentDictionary<string, ConversationEntry> _conversations = new();
    private DateTime _lastCleanup = DateTime.UtcNow;

    public void AddTurn(string conversationId, string question, string answer)
    {
        if (string.IsNullOrWhiteSpace(conversationId)) return;

        var entry = _conversations.GetOrAdd(conversationId, _ => new ConversationEntry());
        lock (entry)
        {
            entry.Turns.Enqueue((question, answer));
            entry.LastAccessedAt = DateTime.UtcNow;
        }

        // Periodically evict idle conversations (non-blocking, best-effort)
        if ((DateTime.UtcNow - _lastCleanup).TotalMinutes >= CleanupIntervalMinutes)
        {
            CleanupIdle();
        }
    }

    /// <summary>
    /// Returns the most recent N turns for a conversation, oldest first.
    /// Returns empty list if conversation does not exist.
    /// </summary>
    public IReadOnlyList<(string Question, string Answer)> GetRecentTurns(
        string conversationId,
        int maxTurns)
    {
        if (string.IsNullOrWhiteSpace(conversationId) || maxTurns <= 0)
            return Array.Empty<(string, string)>();

        if (!_conversations.TryGetValue(conversationId, out var entry))
            return Array.Empty<(string, string)>();

        lock (entry)
        {
            entry.LastAccessedAt = DateTime.UtcNow;

            // Trim queue to maxTurns keeping most recent
            while (entry.Turns.Count > maxTurns)
                entry.Turns.Dequeue();

            return entry.Turns.ToArray();
        }
    }

    private void CleanupIdle()
    {
        _lastCleanup = DateTime.UtcNow;
        var cutoff = DateTime.UtcNow.AddMinutes(-EvictionIdleMinutes);

        foreach (var kvp in _conversations)
        {
            if (kvp.Value.LastAccessedAt < cutoff)
                _conversations.TryRemove(kvp.Key, out _);
        }
    }

    private sealed class ConversationEntry
    {
        public Queue<(string Question, string Answer)> Turns { get; } = new();
        public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;
    }
}
