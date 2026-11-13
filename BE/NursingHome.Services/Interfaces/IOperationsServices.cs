using NursingHome.BusinessObjects.Entities;
using NursingHome.DTOs.Admissions;
using NursingHome.DTOs.Common;
using NursingHome.DTOs.Reports;
using NursingHome.DTOs.Staffing;

namespace NursingHome.Services.Interfaces;

// MODULE VẬN HÀNH & TÀI CHÍNH (Viên): khai báo interface service của module ở file này
// (không thêm vào IServices.cs của phần lõi).

/// <summary>S3-V1 — UC-17, 18, 21, 22: yêu cầu tư vấn, lịch tham quan, phễu khách.</summary>
public interface IAdmissionService
{
    /// <summary>Ẩn danh hoặc chatbot (S4-V1 gọi với <see cref="InquirySource.Chatbot"/>).</summary>
    Task<InquiryDto> CreateInquiryAsync(CreateInquiryRequest request, InquirySource source);

    /// <summary>Ẩn danh: tạo yêu cầu + lịch tham quan, kiểm sức chứa buổi trong transaction.</summary>
    Task<TourBookingDto> BookTourAsync(BookTourRequest request, InquirySource source = InquirySource.Web);

    /// <summary>Số chỗ còn lại mỗi ngày × buổi trong [from, to] (kẹp vào [mai, +60 ngày]).</summary>
    Task<List<TourSlotAvailabilityDto>> GetAvailabilityAsync(DateOnly? from, DateOnly? to);

    Task<List<InquiryDto>> ListInquiriesAsync();

    Task<InquiryDto> UpdateInquiryAsync(CurrentUser user, int id, UpdateInquiryRequest request);

    Task<InquiryDto> AdmitAsync(int id, AdmitInquiryRequest request);

    Task<List<TourBookingDto>> ListToursAsync(DateOnly? from, DateOnly? to);

    Task<TourBookingDto> ConfirmTourAsync(int id, CurrentUser user);

    Task<TourBookingDto> CancelTourAsync(int id);

    Task<TourBookingDto> CompleteTourAsync(int id);

    Task<TourBookingDto> MarkNoShowAsync(int id);
}

/// <summary>S3-V2 — UC-44 lịch ca, UC-45 phân công phụ trách, UC-46 lịch ca của tôi.</summary>
public interface IStaffingService
{
    Task<List<ShiftDto>> ListShiftsAsync();

    /// <summary>Admin/Nurse thấy mọi người; Doctor/Caregiver chỉ ca của mình.</summary>
    Task<List<ShiftAssignmentDto>> ListAssignmentsAsync(CurrentUser user, DateOnly? from, DateOnly? to);

    Task<List<ShiftAssignmentDto>> MyAssignmentsAsync(CurrentUser user, int days);

    Task<ShiftAssignmentDto> CreateAssignmentAsync(CurrentUser user, CreateShiftAssignmentRequest request);

    Task DeleteAssignmentAsync(int id);

    Task<CopyWeekResultDto> CopyWeekAsync(CurrentUser user, CopyWeekRequest request);

    /// <summary>Caregiver chỉ thấy phân công của mình.</summary>
    Task<List<CareAssignmentDto>> ListCareAssignmentsAsync(CurrentUser user, int? residentId, int? accountId, bool activeOnly);

    Task<CareAssignmentDto> CreateCareAssignmentAsync(CurrentUser user, CreateCareAssignmentRequest request);

    Task<CareAssignmentDto> EndCareAssignmentAsync(int id);
}

/// <summary>S3-V3 — UC-123 doanh thu &amp; công nợ, UC-122 công suất giường, UC-124 tuân thủ thuốc. Mỗi báo cáo có bản Excel.</summary>
public interface IReportService
{
    Task<RevenueReportDto> GetRevenueAsync(int? year);

    Task<ExportFile> ExportRevenueAsync(int? year);

    Task<OccupancyReportDto> GetOccupancyAsync(int? year);

    Task<ExportFile> ExportOccupancyAsync(int? year);

    Task<AdherenceReportDto> GetAdherenceAsync(DateOnly? from, DateOnly? to);

    Task<ExportFile> ExportAdherenceAsync(DateOnly? from, DateOnly? to);
}
