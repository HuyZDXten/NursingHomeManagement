using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NursingHomeManagement.Models;
using NursingHomeManagement.Services;

namespace NursingHomeManagement.Data;

public static class DemoScenario
{
    public static async Task SeedAsync(ApplicationDbContext database, UserManager<ApplicationUser> users)
    {
        var resident = await database.Residents.SingleAsync(item => item.Code == "DEMO-001");
        var caregiver = await users.FindByEmailAsync("caregiver@demo.nursinghome.test") ?? throw new InvalidOperationException();
        var admin = await users.FindByEmailAsync("admin@demo.nursinghome.test") ?? throw new InvalidOperationException();
        var clock = new FacilityClock();
        if (!await database.CareTasks.AnyAsync(task => task.ResidentId == resident.Id))
        {
            database.CareTasks.Add(new CareTask { ResidentId = resident.Id, AssignedToId = caregiver.Id, Title = "Demo: hỗ trợ sinh hoạt và ghi nhận sinh hiệu", Instructions = "Tình huống giả lập dành cho kiểm thử.", DueAt = clock.At(clock.Today, new TimeOnly(8, 0)) });
        }

        if (!await database.VitalSigns.AnyAsync(item => item.ResidentId == resident.Id))
        {
            database.VitalSigns.Add(new VitalSign { ResidentId = resident.Id, Systolic = 120, Diastolic = 80, HeartRate = 75, Temperature = 36.5m, SpO2 = 98, Notes = "Số liệu giả lập, không dùng cho điều trị.", RecordedById = caregiver.Id, ShareWithFamily = true });
        }

        if (!await database.FamilyContacts.AnyAsync(item => item.ResidentId == resident.Id))
        {
            database.FamilyContacts.Add(new FamilyContact { ResidentId = resident.Id, FullName = "Người thân minh họa", Relationship = "Con", Phone = "0000000000", IsEmergencyContact = true });
        }

        if (!await database.MedicationSchedules.AnyAsync(item => item.ResidentId == resident.Id))
        {
            database.MedicationSchedules.Add(new MedicationSchedule { ResidentId = resident.Id, MedicationName = "Thuốc mô phỏng — KHÔNG SỬ DỤNG THỰC TẾ", Dose = "Liều mô phỏng", Route = "Mô phỏng", StartDate = clock.Today, ScheduledTime = new TimeOnly(8, 0), ApprovalReference = "DEMO-ONLY — tình huống đã phê duyệt giả lập", EnteredById = admin.Id });
        }

        if (!await database.ResidentSubscriptions.AnyAsync(item => item.ResidentId == resident.Id))
        {
            var package = await database.ServicePackages.SingleAsync(package => package.Name == "Gói demo cơ bản");
            database.ResidentSubscriptions.Add(new ResidentSubscription { ResidentId = resident.Id, ServicePackageId = package.Id, StartDate = clock.Today, AgreedMonthlyPrice = package.MonthlyPrice });
        }

        await database.SaveChangesAsync();
    }
}
