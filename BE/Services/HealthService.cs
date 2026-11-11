using Microsoft.EntityFrameworkCore;
using NursingHomeManagement.Data;
using NursingHomeManagement.Models;
using NursingHomeManagement.ViewModels;

namespace NursingHomeManagement.Services;

public sealed class HealthService(
    ApplicationDbContext database,
    CurrentUser user,
    ResidentAccessService access,
    FacilityClock clock)
{
    public async Task<PagedList<VitalSign>> ListAsync(int residentId, DateOnly? date, int page)
    {
        await user.RequireRoleAsync(Roles.Admin, Roles.Caregiver);
        await access.RequireAsync(user, residentId, ResidentSection.Health);
        var query = database.VitalSigns.AsNoTracking().Include(item => item.RecordedBy)
            .Where(item => item.ResidentId == residentId);
        if (date.HasValue)
        {
            var start = clock.StartOfDay(date.Value);
            var end = start.AddDays(1);
            query = query.Where(item => item.RecordedAt >= start && item.RecordedAt < end);
        }
        return await PagedList<VitalSign>.CreateAsync(query.OrderByDescending(item => item.RecordedAt), page);
    }

    public async Task RecordAsync(HealthForm form)
    {
        await user.RequireRoleAsync(Roles.Admin, Roles.Caregiver);
        await access.RequireAsync(user, form.ResidentId, ResidentSection.Health);
        if (!await database.Residents.AnyAsync(resident => resident.Id == form.ResidentId && resident.Status == ResidentStatus.Active))
        {
            throw new BusinessException("Cư dân đã ngừng lưu trú.");
        }
        if (form.Systolic <= form.Diastolic || form.Systolic is < 40 or > 300 || form.Diastolic is < 20 or > 200 ||
            form.HeartRate is < 20 or > 250 || form.Temperature is < 25 or > 45 || form.SpO2 is < 0 or > 100)
        {
            throw new BusinessException("Giá trị sinh hiệu không hợp lệ; tâm thu phải lớn hơn tâm trương.");
        }
        database.VitalSigns.Add(new VitalSign
        {
            ResidentId = form.ResidentId,
            Systolic = form.Systolic,
            Diastolic = form.Diastolic,
            HeartRate = form.HeartRate,
            Temperature = form.Temperature,
            SpO2 = form.SpO2,
            Notes = form.Notes,
            ShareWithFamily = form.ShareWithFamily,
            RecordedById = user.Id
        });
        await database.SaveChangesAsync();
    }
}
