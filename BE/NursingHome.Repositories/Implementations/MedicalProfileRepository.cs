using NursingHome.BusinessObjects.Entities;
using NursingHome.DataAccessObjects;
using NursingHome.Repositories.Interfaces;

namespace NursingHome.Repositories.Implementations;

public class MedicalProfileRepository : IMedicalProfileRepository
{
    private static MedicalProfileDAO Dao => MedicalProfileDAO.Instance;

    public Task<List<MedicalCondition>> ListConditionsAsync(int residentId) => Dao.ListConditionsAsync(residentId);
    public Task<MedicalCondition?> GetConditionAsync(int id) => Dao.GetConditionAsync(id);
    public Task<MedicalCondition> AddConditionAsync(MedicalCondition condition) => Dao.AddAsync(condition);
    public Task UpdateConditionAsync(MedicalCondition condition) => Dao.UpdateAsync(condition);
    public Task DeleteConditionAsync(MedicalCondition condition) => Dao.DeleteAsync(condition);

    public Task<List<Allergy>> ListAllergiesAsync(int residentId) => Dao.ListAllergiesAsync(residentId);
    public Task<Allergy?> GetAllergyAsync(int id) => Dao.GetAllergyAsync(id);
    public Task<Allergy> AddAllergyAsync(Allergy allergy) => Dao.AddAllergyAsync(allergy);
    public Task DeleteAllergyAsync(Allergy allergy) => Dao.DeleteAllergyAsync(allergy);
    public Task<List<Allergy>> ListDrugAllergiesAsync(int residentId) => Dao.ListDrugAllergiesAsync(residentId);

    public Task<List<DietPlan>> ListDietPlansAsync(int residentId) => Dao.ListDietPlansAsync(residentId);
    public Task<DietPlan> ReplaceDietPlanAsync(DietPlan newPlan) => Dao.ReplaceDietPlanAsync(newPlan);
}
