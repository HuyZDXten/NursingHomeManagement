using NursingHome.BusinessObjects.Entities;
using NursingHome.DTOs.Admissions;
using NursingHome.DTOs.Common;

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
