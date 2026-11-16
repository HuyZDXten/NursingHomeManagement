using NursingHome.BusinessObjects.Entities;
using NursingHome.DataAccessObjects;
using NursingHome.Repositories.Interfaces;

namespace NursingHome.Repositories.Implementations;

public class ClinicalRepository : IClinicalRepository
{
    private static ClinicalDAO Dao => ClinicalDAO.Instance;

    public Task<VitalThreshold?> GetThresholdAsync(int residentId) => Dao.GetThresholdAsync(residentId);
    public Task<VitalThreshold> UpsertThresholdAsync(VitalThreshold input) => Dao.UpsertThresholdAsync(input);

    public Task<List<Assessment>> ListAssessmentsAsync(int residentId) => Dao.ListAssessmentsAsync(residentId);
    public Task<Assessment> AddAssessmentAsync(Assessment item) => Dao.AddAssessmentAsync(item);
    public Task<List<(Resident Resident, AssessmentType Type, Assessment? Latest)>> DueAssessmentsAsync(DateOnly today) =>
        Dao.DueAssessmentsAsync(today);

    public Task<List<DoctorVisit>> ListDoctorVisitsAsync(int residentId) => Dao.ListDoctorVisitsAsync(residentId);
    public Task<DoctorVisit?> GetDoctorVisitAsync(int id) => Dao.GetDoctorVisitAsync(id);
    public Task<DoctorVisit> AddDoctorVisitAsync(DoctorVisit visit) => Dao.AddDoctorVisitAsync(visit);
    public Task<DoctorVisit> UpdateDoctorVisitAsync(DoctorVisit visit) => Dao.UpdateDoctorVisitAsync(visit);
}
