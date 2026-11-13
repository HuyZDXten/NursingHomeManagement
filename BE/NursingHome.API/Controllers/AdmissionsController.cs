using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.AspNetCore.RateLimiting;
using NursingHome.API.Extensions;
using NursingHome.API.Security;
using NursingHome.BusinessObjects.Entities;
using NursingHome.DTOs.Admissions;
using NursingHome.DTOs.Common;
using NursingHome.Services.Interfaces;

namespace NursingHome.API.Controllers;

/// <summary>
/// MODULE VẬN HÀNH (Viên) — S3-V1: UC-17 tư vấn, UC-18 đặt lịch tham quan (công khai), UC-21/22 lễ tân quản lý.
/// Endpoint công khai chỉ nhận dữ liệu và trả số chỗ còn lại, không trả thông tin của người khác.
/// </summary>
[ApiController]
public class AdmissionsController(IAdmissionService admissions) : ControllerBase
{
    // ---------------- Công khai ----------------

    [HttpPost("api/public/inquiries")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.PublicForms)]
    public async Task<ActionResult<MessageResponse>> CreateInquiry([FromBody] CreateInquiryRequest request)
    {
        await admissions.CreateInquiryAsync(request, InquirySource.Web);
        return Ok(new MessageResponse("Đã nhận yêu cầu tư vấn. Lễ tân sẽ liên hệ với bạn sớm."));
    }

    /// <param name="from">yyyy-MM-dd, mặc định ngày mai.</param>
    /// <param name="to">yyyy-MM-dd, mặc định from + 13 ngày; tối đa 60 ngày tới.</param>
    [HttpGet("api/public/tour-slots")]
    [AllowAnonymous]
    public async Task<ActionResult<List<TourSlotAvailabilityDto>>> TourSlots([FromQuery] DateOnly? from, [FromQuery] DateOnly? to) =>
        Ok(await admissions.GetAvailabilityAsync(from, to));

    [HttpPost("api/public/tour-bookings")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.PublicForms)]
    public async Task<ActionResult<MessageResponse>> BookTour([FromBody] BookTourRequest request)
    {
        var tour = await admissions.BookTourAsync(request);
        var slot = tour.TimeSlot == nameof(TourSlot.Morning) ? "buổi sáng" : "buổi chiều";
        return Ok(new MessageResponse($"Đã nhận lịch tham quan ngày {tour.VisitDate:dd/MM/yyyy}, {slot}. Lễ tân sẽ gọi xác nhận."));
    }

    // ---------------- Lễ tân / Admin ----------------

    [HttpPut("api/inquiries/{id:int}")]
    [Authorize(Policy = AuthPolicies.FrontDesk)]
    public async Task<ActionResult<InquiryDto>> UpdateInquiry(int id, [FromBody] UpdateInquiryRequest request) =>
        Ok(await admissions.UpdateInquiryAsync(User.RequireCurrentUser(), id, request));

    /// <summary>Gắn cư dân vừa tạo bằng POST /api/residents; yêu cầu phải ở trạng thái Visited.</summary>
    [HttpPost("api/inquiries/{id:int}/admit")]
    [Authorize(Policy = AuthPolicies.FrontDesk)]
    public async Task<ActionResult<InquiryDto>> Admit(int id, [FromBody] AdmitInquiryRequest request) =>
        Ok(await admissions.AdmitAsync(id, request));

    /// <param name="from">yyyy-MM-dd, mặc định hôm nay.</param>
    /// <param name="to">yyyy-MM-dd, mặc định from + 6 ngày.</param>
    [HttpGet("api/tour-bookings")]
    [Authorize(Policy = AuthPolicies.FrontDesk)]
    public async Task<ActionResult<List<TourBookingDto>>> Tours([FromQuery] DateOnly? from, [FromQuery] DateOnly? to) =>
        Ok(await admissions.ListToursAsync(from, to));

    [HttpPost("api/tour-bookings/{id:int}/confirm")]
    [Authorize(Policy = AuthPolicies.FrontDesk)]
    public async Task<ActionResult<TourBookingDto>> Confirm(int id) => Ok(await admissions.ConfirmTourAsync(id, User.RequireCurrentUser()));

    [HttpPost("api/tour-bookings/{id:int}/cancel")]
    [Authorize(Policy = AuthPolicies.FrontDesk)]
    public async Task<ActionResult<TourBookingDto>> Cancel(int id) => Ok(await admissions.CancelTourAsync(id));

    [HttpPost("api/tour-bookings/{id:int}/done")]
    [Authorize(Policy = AuthPolicies.FrontDesk)]
    public async Task<ActionResult<TourBookingDto>> Done(int id) => Ok(await admissions.CompleteTourAsync(id));

    [HttpPost("api/tour-bookings/{id:int}/no-show")]
    [Authorize(Policy = AuthPolicies.FrontDesk)]
    public async Task<ActionResult<TourBookingDto>> NoShow(int id) => Ok(await admissions.MarkNoShowAsync(id));
}

/// <summary>GET /odata/Inquiries?$filter=status eq 'New'&amp;$orderby=createdAt desc&amp;$count=true</summary>
[ApiController]
[Route("odata/Inquiries")]
[Authorize(Policy = AuthPolicies.FrontDesk)]
public class InquiriesODataController(IAdmissionService admissions) : ODataController
{
    [HttpGet]
    [EnableQuery]
    public async Task<ActionResult<IQueryable<InquiryDto>>> Get() => Ok((await admissions.ListInquiriesAsync()).AsQueryable());
}
