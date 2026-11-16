using NursingHome.BusinessObjects.Entities;

namespace NursingHome.Repositories.Interfaces;

/// <summary>MODULE VẬN HÀNH (Viên) — S4-V1 hạ tầng AI (dùng chung với Huy).</summary>
public interface IAiRepository
{
    Task<AiConversation> GetOrCreateConversationAsync(string channel, int? accountId, string? anonymousKey, DateTime now);

    Task<AiConversation?> FindConversationAsync(string channel, int? accountId, string? anonymousKey);

    Task<List<AiMessageRecord>> ListMessagesAsync(long conversationId, int take, bool includeTools);

    Task AddMessagesAsync(long conversationId, IReadOnlyCollection<AiMessageRecord> messages, string? title, DateTime now);

    Task<List<AiKnowledgeDoc>> ListKnowledgeAsync();

    Task<AiKnowledgeDoc?> GetKnowledgeAsync(int id);

    Task<AiKnowledgeDoc> AddKnowledgeAsync(AiKnowledgeDoc doc);

    Task UpdateKnowledgeAsync(AiKnowledgeDoc doc);

    Task DeleteKnowledgeAsync(int id);

    Task<List<AiKnowledgeDoc>> SearchKnowledgeAsync(IReadOnlyCollection<string> keywords, bool publicOnly, int take);
}
