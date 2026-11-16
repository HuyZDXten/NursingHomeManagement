using NursingHome.BusinessObjects.Entities;

namespace NursingHome.Repositories.Interfaces;

/// <summary>S3-H2: bệnh nền, dị ứng, chế độ ăn.</summary>
public interface IMedicalProfileRepository
{
    Task<List<MedicalCondition>> ListConditionsAsync(int residentId);
    Task<MedicalCondition?> GetConditionAsync(int id);
    Task<MedicalCondition> AddConditionAsync(MedicalCondition condition);
    Task UpdateConditionAsync(MedicalCondition condition);
    Task DeleteConditionAsync(MedicalCondition condition);

    Task<List<Allergy>> ListAllergiesAsync(int residentId);
    Task<Allergy?> GetAllergyAsync(int id);
    Task<Allergy> AddAllergyAsync(Allergy allergy);
    Task DeleteAllergyAsync(Allergy allergy);
    Task<List<Allergy>> ListDrugAllergiesAsync(int residentId);

    Task<List<DietPlan>> ListDietPlansAsync(int residentId);
    Task<DietPlan> ReplaceDietPlanAsync(DietPlan newPlan);
}
