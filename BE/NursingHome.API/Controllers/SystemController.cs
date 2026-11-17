using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingHome.API.Extensions;
using NursingHome.API.Filters;
using NursingHome.API.Security;
using NursingHome.BusinessObjects.Enums;
using NursingHome.DTOs.Admin;
using NursingHome.Services.Interfaces;

namespace NursingHome.API.Controllers;

/// <summary>BL-V5 quản trị hệ thống: nhật ký thao tác (UC-13), cấu hình cơ sở (UC-129), danh mục (UC-130). Chỉ Admin sửa; danh mục ai đăng nhập cũng đọc.</summary>
[ApiController]
[Authorize]
public class SystemController(IAuditService audit, ISettingsService settings, ILookupService lookups) : ControllerBase
{
    // ---------------- UC-13 nhật ký thao tác ----------------

    /// <summary>Lọc theo ngày (giờ Việt Nam), người thực hiện, thao tác, loại đối tượng, từ khóa; phân trang (tối đa 100 dòng/trang).</summary>
    [HttpGet("api/audit-logs")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public async Task<ActionResult<PageDto<AuditLogDto>>> AuditLogs([FromQuery] AuditQuery query) => Ok(await audit.ListAsync(query));

    [HttpGet("api/audit-logs/actions")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public async Task<ActionResult<List<string>>> AuditActions() => Ok(await audit.ListActionsAsync());

    // ---------------- UC-129 cấu hình cơ sở ----------------

    [HttpGet("api/settings")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public async Task<ActionResult<List<SettingDto>>> Settings() => Ok(await settings.ListAsync());

    /// <summary>Lưu một nhóm khóa trong một lần (hoặc lưu hết hoặc không lưu gì). Áp dụng ngay, không cần khởi động lại API.</summary>
    [HttpPut("api/settings")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public async Task<ActionResult<List<SettingDto>>> SaveSettings([FromBody] SaveSettingsRequest request) =>
        Ok(await settings.SaveAsync(User.RequireCurrentUser(), request));

    /// <summary>Bỏ ghi đè một khóa, quay về giá trị trong appsettings (khóa dạng Facility:Name nên truyền qua query).</summary>
    [HttpDelete("api/settings")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public async Task<ActionResult<List<SettingDto>>> ResetSetting([FromQuery] string key) =>
        Ok(await settings.ResetAsync(User.RequireCurrentUser(), key));

    // ---------------- UC-130 danh mục ----------------

    /// <summary>category = Relationship | Department | Position. Chỉ Admin thấy cả mục đã ẩn (includeInactive).</summary>
    [HttpGet("api/lookups")]
    public async Task<ActionResult<List<LookupDto>>> Lookups([FromQuery] string? category, [FromQuery] bool includeInactive = false) =>
        Ok(await lookups.ListAsync(category, includeInactive && User.IsInRole(Roles.Admin)));

    [HttpPost("api/lookups")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    [Audit("Lookup.Create", "Lookup", "Thêm mục danh mục")]
    public async Task<ActionResult<LookupDto>> CreateLookup([FromBody] SaveLookupRequest request) => Ok(await lookups.CreateAsync(request));

    [HttpPut("api/lookups/{id:int}")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    [Audit("Lookup.Update", "Lookup", "Sửa mục danh mục")]
    public async Task<ActionResult<LookupDto>> UpdateLookup(int id, [FromBody] SaveLookupRequest request) => Ok(await lookups.UpdateAsync(id, request));

    [HttpDelete("api/lookups/{id:int}")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    [Audit("Lookup.Delete", "Lookup", "Xóa mục danh mục")]
    public async Task<IActionResult> DeleteLookup(int id)
    {
        await lookups.DeleteAsync(id);
        return NoContent();
    }
}
