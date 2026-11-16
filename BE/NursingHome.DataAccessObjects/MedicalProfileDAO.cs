using System.Data;
using Microsoft.EntityFrameworkCore;
using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Exceptions;

namespace NursingHome.DataAccessObjects;

/// <summary>S3-H2 (UC-66, UC-71, UC-82): bệnh nền, dị ứng, chế độ ăn.</summary>
public sealed class MedicalProfileDAO : GenericDAO<MedicalCondition>
{
    public static MedicalProfileDAO Instance { get; } = new();

    private MedicalProfileDAO()
    {
    }

    public async Task<List<MedicalCondition>> ListConditionsAsync(int residentId)
    {
        await using var context = CreateContext();
        return await context.MedicalConditions.AsNoTracking()
            .Include(item => item.RecordedBy)
            .Where(item => item.ResidentId == residentId)
            .OrderBy(item => item.Status)
            .ThenByDescending(item => item.CreatedAt)
            .ToListAsync();
    }

    public async Task<MedicalCondition?> GetConditionAsync(int id)
    {
        await using var context = CreateContext();
        return await context.MedicalConditions.Include(item => item.RecordedBy)
            .FirstOrDefaultAsync(item => item.Id == id);
    }

    public async Task<List<Allergy>> ListAllergiesAsync(int residentId)
    {
        await using var context = CreateContext();
        return await context.Allergies.AsNoTracking()
            .Include(item => item.RecordedBy)
            .Where(item => item.ResidentId == residentId)
            .OrderByDescending(item => item.Severity)
            .ThenBy(item => item.Allergen)
            .ToListAsync();
    }

    public async Task<Allergy?> GetAllergyAsync(int id)
    {
        await using var context = CreateContext();
        return await context.Allergies.Include(item => item.RecordedBy)
            .FirstOrDefaultAsync(item => item.Id == id);
    }

    public Task<Allergy> AddAllergyAsync(Allergy allergy) => AddEntityAsync(allergy);

    public Task DeleteAllergyAsync(Allergy allergy) => DeleteEntityAsync(allergy);

    public async Task<List<Allergy>> ListDrugAllergiesAsync(int residentId)
    {
        await using var context = CreateContext();
        return await context.Allergies.AsNoTracking()
            .Where(item => item.ResidentId == residentId && item.Type == AllergyType.Drug)
            .ToListAsync();
    }

    public async Task<List<DietPlan>> ListDietPlansAsync(int residentId)
    {
        await using var context = CreateContext();
        return await context.DietPlans.AsNoTracking()
            .Include(item => item.PrescribedBy)
            .Where(item => item.ResidentId == residentId)
            .OrderByDescending(item => item.EndDate == null)
            .ThenByDescending(item => item.StartDate)
            .ThenByDescending(item => item.Id)
            .ToListAsync();
    }

    /// <summary>
    /// Kết thúc chế độ ăn đang mở (EndDate = StartDate mới - 1) và thêm chế độ mới trong cùng transaction
    /// để luôn thỏa mãn filtered unique index UX_DietPlans_Active.
    /// </summary>
    public async Task<DietPlan> ReplaceDietPlanAsync(DietPlan newPlan)
    {
        await using var context = CreateContext();
        await using var tx = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        var active = await context.DietPlans
            .FirstOrDefaultAsync(item => item.ResidentId == newPlan.ResidentId && item.EndDate == null);
        if (active is not null)
        {
            if (newPlan.StartDate <= active.StartDate)
            {
                throw new BusinessRuleException(
                    $"Ngày bắt đầu chế độ mới phải sau ngày bắt đầu chế độ hiện tại ({active.StartDate:dd/MM/yyyy}).");
            }

            active.EndDate = newPlan.StartDate.AddDays(-1);
            await context.SaveChangesAsync();
        }

        context.DietPlans.Add(newPlan);
        await context.SaveChangesAsync();
        await tx.CommitAsync();

        return await context.DietPlans.AsNoTracking()
            .Include(item => item.PrescribedBy)
            .FirstAsync(item => item.Id == newPlan.Id);
    }
}
