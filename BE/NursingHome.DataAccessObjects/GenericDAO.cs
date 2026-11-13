using Microsoft.EntityFrameworkCore;
using NursingHome.BusinessObjects;

namespace NursingHome.DataAccessObjects;

/// <summary>
/// CRUD dùng chung cho các DAO. Mỗi lệnh mở một DbContext ngắn hạn rồi dispose ngay (giống ASS2),
/// nên các DAO singleton dùng chung an toàn giữa nhiều request.
/// Thao tác cần nhiều bước nhất quán (chuyển giường, thanh toán...) viết thành MỘT method DAO
/// chạy trọn trong một transaction, không ghép nhiều lời gọi DAO ở Service.
/// </summary>
public abstract class GenericDAO<T> where T : class
{
    protected static NursingHomeContext CreateContext() => new();

    public async Task<List<T>> GetAllAsync()
    {
        await using var context = CreateContext();
        return await context.Set<T>().AsNoTracking().ToListAsync();
    }

    public async Task<T?> GetByIdAsync(params object[] keyValues)
    {
        await using var context = CreateContext();
        return await context.Set<T>().FindAsync(keyValues);
    }

    public Task<T> AddAsync(T entity) => AddEntityAsync(entity);

    public Task UpdateAsync(T entity) => UpdateEntityAsync(entity);

    public Task DeleteAsync(T entity) => DeleteEntityAsync(entity);

    // ---- Dùng cho entity phụ trong cùng module (ví dụ FamilyContact trong ResidentDAO). ----

    protected static async Task<TEntity> AddEntityAsync<TEntity>(TEntity entity) where TEntity : class
    {
        await using var context = CreateContext();
        context.Set<TEntity>().Add(entity);
        await context.SaveChangesAsync();
        return entity;
    }

    /// <summary>Entity có RowVersion: giá trị đang giữ được dùng làm điều kiện chống ghi đè.</summary>
    protected static async Task UpdateEntityAsync<TEntity>(TEntity entity) where TEntity : class
    {
        await using var context = CreateContext();
        context.Set<TEntity>().Update(entity);
        await context.SaveChangesAsync();
    }

    protected static async Task DeleteEntityAsync<TEntity>(TEntity entity) where TEntity : class
    {
        await using var context = CreateContext();
        context.Set<TEntity>().Attach(entity);
        context.Set<TEntity>().Remove(entity);
        await context.SaveChangesAsync();
    }
}
