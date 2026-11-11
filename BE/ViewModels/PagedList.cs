using Microsoft.EntityFrameworkCore;

namespace NursingHomeManagement.ViewModels;

public sealed class PagedList<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public int Page
    {
        get; init;
    }
    public int TotalPages
    {
        get; init;
    }
    public int TotalCount
    {
        get; init;
    }

    public static async Task<PagedList<T>> CreateAsync(IQueryable<T> query, int page, int pageSize = 10)
    {
        int count = await query.CountAsync();
        int totalPages = Math.Max(1, (int)Math.Ceiling(count / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);
        return new PagedList<T>
        {
            Items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(),
            Page = page,
            TotalPages = totalPages,
            TotalCount = count
        };
    }
}
