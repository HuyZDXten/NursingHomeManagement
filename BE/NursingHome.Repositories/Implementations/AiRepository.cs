using NursingHome.BusinessObjects.Entities;
using NursingHome.DataAccessObjects;
using NursingHome.Repositories.Interfaces;

namespace NursingHome.Repositories.Implementations;

public class AiRepository : IAiRepository
{
    private static AiDAO Dao => AiDAO.Instance;

    public Task<AiConversation> GetOrCreateConversationAsync(string channel, int? accountId, string? anonymousKey, DateTime now) =>
        Dao.GetOrCreateConversationAsync(channel, accountId, anonymousKey, now);

    public Task<AiConversation?> FindConversationAsync(string channel, int? accountId, string? anonymousKey) =>
        Dao.FindConversationAsync(channel, accountId, anonymousKey);

    public Task<List<AiMessageRecord>> ListMessagesAsync(long conversationId, int take, bool includeTools) =>
        Dao.ListMessagesAsync(conversationId, take, includeTools);

    public Task AddMessagesAsync(long conversationId, IReadOnlyCollection<AiMessageRecord> messages, string? title, DateTime now) =>
        Dao.AddMessagesAsync(conversationId, messages, title, now);

    public Task<List<AiKnowledgeDoc>> ListKnowledgeAsync() => Dao.ListKnowledgeAsync();

    public Task<AiKnowledgeDoc?> GetKnowledgeAsync(int id) => Dao.GetKnowledgeAsync(id);

    public Task<AiKnowledgeDoc> AddKnowledgeAsync(AiKnowledgeDoc doc) => Dao.AddKnowledgeAsync(doc);

    public Task UpdateKnowledgeAsync(AiKnowledgeDoc doc) => Dao.UpdateKnowledgeAsync(doc);

    public Task DeleteKnowledgeAsync(int id) => Dao.DeleteKnowledgeAsync(id);

    public Task<List<AiKnowledgeDoc>> SearchKnowledgeAsync(IReadOnlyCollection<string> keywords, bool publicOnly, int take) =>
        Dao.SearchKnowledgeAsync(keywords, publicOnly, take);
}
