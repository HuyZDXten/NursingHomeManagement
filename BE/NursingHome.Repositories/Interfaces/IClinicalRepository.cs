using NursingHome.BusinessObjects.Entities;

namespace NursingHome.Repositories.Interfaces;

/// <summary>S3-H5: ngưỡng sinh hiệu riêng, đánh giá định kỳ, phiếu khám bác sĩ.</summary>
public interface IClinicalRepository
{
    Task<VitalThreshold?> GetThresholdAsync(int residentId);
    Task<VitalThreshold> UpsertThresholdAsync(VitalThreshold input);

    Task<List<Assessment>> ListAssessmentsAsync(int residentId);
    Task<Assessment> AddAssessmentAsync(Assessment item);
    Task<List<(Resident Resident, AssessmentType Type, Assessment? Latest)>> DueAssessmentsAsync(DateOnly today);

    Task<List<DoctorVisit>> ListDoctorVisitsAsync(int residentId);
    Task<DoctorVisit?> GetDoctorVisitAsync(int id);
    Task<DoctorVisit> AddDoctorVisitAsync(DoctorVisit visit);
    Task<DoctorVisit> UpdateDoctorVisitAsync(DoctorVisit visit);
}
