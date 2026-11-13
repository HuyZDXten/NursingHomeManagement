using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Exceptions;

namespace NursingHome.DataAccessObjects;

/// <summary>MODULE VẬN HÀNH (Viên) — S4-V1: hội thoại AI, tin nhắn AI, tài liệu tri thức (dùng chung với S4-H1/H2 của Huy).</summary>
public sealed class AiDAO : GenericDAO<AiConversation>
{
    public static AiDAO Instance { get; } = new();

    private AiDAO()
    {
    }

    // ---------------- Hội thoại ----------------

    /// <summary>
    /// Public: theo <paramref name="anonymousKey"/>; Family/Staff: theo (tài khoản, kênh). Hai request cùng tạo →
    /// unique index chặn một bên, bên đó đọc lại.
    /// </summary>
    public async Task<AiConversation> GetOrCreateConversationAsync(string channel, int? accountId, string? anonymousKey, DateTime now)
    {
        var existing = await FindConversationAsync(channel, accountId, anonymousKey);
        if (existing is not null)
        {
            return existing;
        }

        try
        {
            await using var context = CreateContext();
            context.AiConversations.Add(new AiConversation { Channel = channel, AccountId = accountId, AnonymousKey = anonymousKey, CreatedAt = now });
            await context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // Request khác vừa tạo — dùng lại.
        }

        return await FindConversationAsync(channel, accountId, anonymousKey) ?? throw new InvalidOperationException("Không tạo được hội thoại AI.");
    }

    public async Task<AiConversation?> FindConversationAsync(string channel, int? accountId, string? anonymousKey)
    {
        await using var context = CreateContext();
        return await context.AiConversations.AsNoTracking().FirstOrDefaultAsync(item =>
            anonymousKey != null
                ? item.AnonymousKey == anonymousKey && item.Channel == channel
                : item.AccountId == accountId && item.Channel == channel);
    }

    /// <summary><paramref name="take"/> tin gần nhất (cũ → mới). <paramref name="includeTools"/> = false: chỉ user/assistant.</summary>
    public async Task<List<AiMessageRecord>> ListMessagesAsync(long conversationId, int take, bool includeTools)
    {
        await using var context = CreateContext();
        var page = await context.AiMessages.AsNoTracking()
            .Where(item => item.ConversationId == conversationId && (includeTools || item.Role != AiRoles.Tool))
            .OrderByDescending(item => item.Id)
            .Take(take)
            .ToListAsync();
        page.Reverse();
        return page;
    }

    /// <summary>Lưu một lượt hỏi–đáp (tin user, kết quả tool, câu trả lời) và cập nhật LastMessageAt/Title trong một SaveChanges.</summary>
    public async Task AddMessagesAsync(long conversationId, IReadOnlyCollection<AiMessageRecord> messages, string? title, DateTime now)
    {
        await using var context = CreateContext();
        var conversation = await context.AiConversations.FindAsync(conversationId)
            ?? throw new NotFoundException("Không tìm thấy hội thoại AI.");
        conversation.LastMessageAt = now;
        conversation.Title ??= title;
        foreach (var message in messages)
        {
            message.ConversationId = conversationId;
            context.AiMessages.Add(message);
        }

        await context.SaveChangesAsync();
    }

    // ---------------- Tri thức ----------------

    public async Task<List<AiKnowledgeDoc>> ListKnowledgeAsync()
    {
        await using var context = CreateContext();
        return await context.AiKnowledgeDocs.AsNoTracking().OrderBy(item => item.Title).ToListAsync();
    }

    public async Task<AiKnowledgeDoc?> GetKnowledgeAsync(int id)
    {
        await using var context = CreateContext();
        return await context.AiKnowledgeDocs.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id);
    }

    public Task<AiKnowledgeDoc> AddKnowledgeAsync(AiKnowledgeDoc doc) => AddEntityAsync(doc);

    public Task UpdateKnowledgeAsync(AiKnowledgeDoc doc) => UpdateEntityAsync(doc);

    public async Task DeleteKnowledgeAsync(int id)
    {
        await using var context = CreateContext();
        if (await context.AiKnowledgeDocs.Where(item => item.Id == id).ExecuteDeleteAsync() == 0)
        {
            throw new NotFoundException("Không tìm thấy tài liệu.");
        }
    }

    /// <summary>
    /// Tìm tài liệu đang dùng theo từ khóa: điểm = từ khớp tiêu đề ×3 + thẻ ×2 + nội dung ×1, lấy <paramref name="take"/> tài liệu
    /// điểm cao nhất. Lọc trong bộ nhớ (không phân biệt hoa thường, theo culture) — đủ cho vài chục tài liệu FAQ, không cần full-text index.
    /// </summary>
    public async Task<List<AiKnowledgeDoc>> SearchKnowledgeAsync(IReadOnlyCollection<string> keywords, bool publicOnly, int take)
    {
        if (keywords.Count == 0)
        {
            return [];
        }

        await using var context = CreateContext();
        var candidates = await context.AiKnowledgeDocs.AsNoTracking()
            .Where(item => item.IsActive && (!publicOnly || item.IsPublic))
            .ToListAsync();
        return candidates
            .Select(doc => (Doc: doc, Score: keywords.Sum(keyword =>
                Contains(doc.Title, keyword) * 3 + Contains(doc.Tags, keyword) * 2 + Contains(doc.Content, keyword))))
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score).ThenBy(item => item.Doc.Title)
            .Take(take)
            .Select(item => item.Doc)
            .ToList();
    }

    private static int Contains(string? text, string keyword) =>
        text is not null && text.Contains(keyword, StringComparison.CurrentCultureIgnoreCase) ? 1 : 0;
}
