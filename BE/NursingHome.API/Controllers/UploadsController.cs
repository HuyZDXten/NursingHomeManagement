using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingHome.API.Extensions;
using NursingHome.API.Security;
using NursingHome.DTOs.Accounts;
using NursingHome.Services.Helpers;
using NursingHome.Services.Interfaces;

namespace NursingHome.API.Controllers;

/// <summary>
/// BL-V1: tải ảnh dùng chung cho nhân viên, trả URL để lưu vào bảng của tính năng khác
/// (Huy dùng cho ảnh đính kèm ghi chép chăm sóc UC-60: folder = "care").
/// </summary>
[ApiController]
[Route("api/uploads")]
[Authorize(Policy = AuthPolicies.Staff)]
public class UploadsController(IMediaService media) : ControllerBase
{
    /// <summary>multipart: "file" (JPG/PNG/WebP ≤ 5 MB), query folder = images (mặc định) | care.</summary>
    [HttpPost("image")]
    [RequestSizeLimit(AuthController.UploadLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = AuthController.UploadLimit)]
    public async Task<ActionResult<UploadedImageDto>> Image(IFormFile? file, [FromQuery] string? folder)
    {
        string target = string.IsNullOrWhiteSpace(folder) ? ImageRules.GeneralFolder : folder.Trim().ToLowerInvariant();
        if (target is not (ImageRules.GeneralFolder or ImageRules.CareFolder))
        {
            return BadRequest(new { message = "folder chỉ nhận images hoặc care." });
        }

        await using var content = await file.ReadImageAsync();
        return Ok(await media.UploadImageAsync(content, file!.FileName, file.Length, target));
    }
}
