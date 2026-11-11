using Microsoft.EntityFrameworkCore;
using NursingHomeManagement.Data;
using NursingHomeManagement.Models;
using NursingHomeManagement.ViewModels;

namespace NursingHomeManagement.Services;

public sealed class CareTaskService(
    ApplicationDbContext database,
    CurrentUser user,
    FacilityClock clock)
{
    public async Task<PagedList<CareTask>> ListAsync(DateOnly? date, CareTaskStatus? status, int page)
    {
        await user.RequireRoleAsync(Roles.Admin, Roles.Caregiver);
        var query = database.CareTasks.AsNoTracking().Include(task => task.Resident).Include(task => task.AssignedTo).AsQueryable();
        if (user.IsInRole(Roles.Caregiver))
        {
            query = query.Where(task => task.AssignedToId == user.Id);
        }
        if (date.HasValue)
        {
            var start = clock.StartOfDay(date.Value);
            var end = start.AddDays(1);
            query = query.Where(task => task.DueAt >= start && task.DueAt < end);
        }
        if (status.HasValue)
        {
            query = query.Where(task => task.Status == status);
        }
        return await PagedList<CareTask>.CreateAsync(query.OrderBy(task => task.DueAt), page);
    }

    public async Task<CareTask> GetAsync(int id)
    {
        await user.RequireRoleAsync(Roles.Admin, Roles.Caregiver);
        var task = await database.CareTasks.AsNoTracking().Include(item => item.Resident).Include(item => item.AssignedTo)
            .SingleOrDefaultAsync(item => item.Id == id) ?? throw new KeyNotFoundException();
        if (user.IsInRole(Roles.Caregiver) && task.AssignedToId != user.Id)
        {
            throw new UnauthorizedAccessException();
        }
        return task;
    }

    public async Task<List<CareRecord>> HistoryAsync(int id)
    {
        await GetAsync(id);
        return await database.CareRecords.AsNoTracking().Include(record => record.RecordedBy)
            .Where(record => record.CareTaskId == id).OrderByDescending(record => record.PerformedAt).ToListAsync();
    }

    public async Task CreateAsync(CareTaskForm form)
    {
        await user.RequireRoleAsync(Roles.Admin);
        if (!await database.Residents.AnyAsync(resident => resident.Id == form.ResidentId && resident.Status == ResidentStatus.Active))
        {
            throw new BusinessException("Vui lòng chọn cư dân đang lưu trú.");
        }
        bool caregiver = await (from account in database.Users
                                join membership in database.UserRoles on account.Id equals membership.UserId
                                join role in database.Roles on membership.RoleId equals role.Id
                                where account.Id == form.AssignedToId && account.IsActive && role.Name == Roles.Caregiver
                                select account.Id).AnyAsync();
        if (!caregiver)
        {
            throw new BusinessException("Người được phân công phải là Caregiver đang hoạt động.");
        }
        database.CareTasks.Add(new CareTask
        {
            ResidentId = form.ResidentId,
            AssignedToId = form.AssignedToId,
            Title = form.Title.Trim(),
            Instructions = form.Instructions,
            DueAt = clock.At(form.Date, form.Time)
        });
        await database.SaveChangesAsync();
    }

    public async Task UpdateAsync(int id, CareUpdateForm form)
    {
        await GetAsync(id);
        var task = await database.CareTasks.FindAsync(id) ?? throw new KeyNotFoundException();
        if (task.Status is CareTaskStatus.Completed or CareTaskStatus.Cancelled)
        {
            throw new BusinessException("Công việc đã kết thúc, không thể sửa lịch sử.");
        }
        if (form.Status is not (CareTaskStatus.InProgress or CareTaskStatus.Completed or CareTaskStatus.Cancelled) ||
            string.IsNullOrWhiteSpace(form.Result))
        {
            throw new BusinessException("Chọn trạng thái hợp lệ và nhập kết quả hoặc lý do.");
        }
        if (form.Status == CareTaskStatus.Cancelled && !user.IsInRole(Roles.Admin))
        {
            throw new BusinessException("Chỉ Admin được hủy công việc.");
        }
        if (!Convert.TryFromBase64String(form.RowVersion, new byte[8], out int length) || length != 8)
        {
            throw new BusinessException("Phiên bản dữ liệu không hợp lệ. Vui lòng tải lại trang.");
        }
        database.Entry(task).Property(item => item.RowVersion).OriginalValue = Convert.FromBase64String(form.RowVersion);
        task.Status = form.Status;
        database.CareRecords.Add(new CareRecord
        {
            CareTaskId = id,
            Status = form.Status,
            Result = form.Result.Trim(),
            ShareWithFamily = form.ShareWithFamily,
            RecordedById = user.Id
        });
        await database.SaveChangesAsync();
    }
}
