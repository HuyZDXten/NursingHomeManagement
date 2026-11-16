using NursingHome.DTOs.Care;
using NursingHome.DTOs.Common;

namespace NursingHome.Services.Interfaces;

// MODULE CHĂM SÓC & GIA ĐÌNH (Huy): khai báo interface service của module ở file này
// (không thêm vào IServices.cs của phần lõi).

/// <summary>S3-H1 (UC-58, UC-59, UC-107): báo cáo và xử lý sự cố.</summary>
public interface IIncidentService
{
    Task<List<IncidentDto>> ListAsync(CurrentUser user, int? residentId, string? status, string? severity, DateOnly? from, DateOnly? to);

    Task<IncidentDto> GetAsync(CurrentUser user, int id);

    Task<IncidentDto> ReportAsync(CurrentUser user, ReportIncidentRequest request);

    Task<IncidentDto> UpdateAsync(CurrentUser user, int id, UpdateIncidentRequest request);

    Task<List<FamilyIncidentDto>> ListForFamilyAsync(CurrentUser user, int residentId);
}
