using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingHome.API.Extensions;
using NursingHome.API.Filters;
using NursingHome.API.Security;
using NursingHome.BusinessObjects.Exceptions;
using NursingHome.DTOs.Residents;
using NursingHome.Services.Helpers;
using NursingHome.Services.Interfaces;

namespace NursingHome.API.Controllers;

/// <summary>
/// BL-V2 hồ sơ lưu trú: giấy tờ (UC-29), hợp đồng (UC-30), tạm vắng (UC-31), tái nhập viện (UC-33), tiền cọc (UC-99).
/// File tải lên dạng multipart (trường "file"); file chỉ tải về qua endpoint này (lưu ngoài wwwroot).
/// </summary>
[ApiController]
[Authorize] // policy đặt từng endpoint: hợp đồng mở cho cả gia đình có quyền hóa đơn (InvoiceReaders)
public class ResidentRecordsController(IResidentRecordsService records) : ControllerBase
{
    private const long UploadLimit = 12 * 1024 * 1024;

    // ---------------- Giấy tờ: chỉ lễ tân, Admin (CCCD, BHYT là dữ liệu nhạy cảm) ----------------

    [HttpGet("api/residents/{residentId:int}/documents")]
    [Authorize(Policy = AuthPolicies.FrontDesk)]
    public async Task<ActionResult<List<ResidentDocumentDto>>> Documents(int residentId) => Ok(await records.ListDocumentsAsync(residentId));

    [HttpPost("api/residents/{residentId:int}/documents")]
    [Authorize(Policy = AuthPolicies.FrontDesk)]
    [RequestSizeLimit(UploadLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadLimit)]
    public async Task<ActionResult<ResidentDocumentDto>> UploadDocument(int residentId, [FromForm] UploadDocumentRequest request, IFormFile? file)
    {
        await using var content = await ReadAsync(file);
        return Ok(await records.UploadDocumentAsync(User.RequireCurrentUser(), residentId, request, content, file!.FileName, file.Length));
    }

    [HttpGet("api/residents/{residentId:int}/documents/{documentId:int}/file")]
    [Authorize(Policy = AuthPolicies.FrontDesk)]
    public async Task<IActionResult> DocumentFile(int residentId, int documentId) => Download(await records.OpenDocumentAsync(residentId, documentId));

    [HttpDelete("api/residents/{residentId:int}/documents/{documentId:int}")]
    [Authorize(Policy = AuthPolicies.FrontDesk)]
    [Audit("ResidentDocument.Delete", "Resident", "Xóa giấy tờ của cư dân", "residentId")]
    public async Task<IActionResult> DeleteDocument(int residentId, int documentId)
    {
        await records.DeleteDocumentAsync(residentId, documentId);
        return NoContent();
    }

    // ---------------- Hợp đồng: xem theo quyền phần Billing (có cả gia đình), thêm/kết thúc: lễ tân, Admin ----------------

    [HttpGet("api/residents/{residentId:int}/contracts")]
    [Authorize(Policy = AuthPolicies.InvoiceReaders)]
    public async Task<ActionResult<List<ResidentContractDto>>> Contracts(int residentId) =>
        Ok(await records.ListContractsAsync(User.RequireCurrentUser(), residentId));

    /// <summary>multipart: các trường của CreateContractRequest + "file" (PDF/JPG/PNG ≤ 10 MB, không bắt buộc).</summary>
    [HttpPost("api/residents/{residentId:int}/contracts")]
    [Authorize(Policy = AuthPolicies.FrontDesk)]
    [RequestSizeLimit(UploadLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadLimit)]
    public async Task<ActionResult<ResidentContractDto>> CreateContract(int residentId, [FromForm] CreateContractRequest request, IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            return Ok(await records.CreateContractAsync(User.RequireCurrentUser(), residentId, request, null, null, 0));
        }

        await using var content = await ReadAsync(file);
        return Ok(await records.CreateContractAsync(User.RequireCurrentUser(), residentId, request, content, file.FileName, file.Length));
    }

    [HttpPost("api/residents/{residentId:int}/contracts/{contractId:int}/end")]
    [Authorize(Policy = AuthPolicies.FrontDesk)]
    [Audit("Contract.End", "Resident", "Kết thúc hợp đồng của cư dân", "residentId")]
    public async Task<ActionResult<ResidentContractDto>> EndContract(int residentId, int contractId, [FromBody] EndContractRequest request) =>
        Ok(await records.EndContractAsync(residentId, contractId, request));

    [HttpGet("api/residents/{residentId:int}/contracts/{contractId:int}/file")]
    [Authorize(Policy = AuthPolicies.InvoiceReaders)]
    public async Task<IActionResult> ContractFile(int residentId, int contractId) =>
        Download(await records.OpenContractFileAsync(User.RequireCurrentUser(), residentId, contractId));

    // ---------------- Tạm vắng: xem theo quyền xem cư dân; ghi: lễ tân, Admin, điều dưỡng ----------------

    [HttpGet("api/resident-leaves/open")]
    [Authorize(Policy = AuthPolicies.Staff)]
    public async Task<ActionResult<List<ResidentLeaveDto>>> OpenLeaves() => Ok(await records.OpenLeavesAsync(User.RequireCurrentUser()));

    [HttpGet("api/residents/{residentId:int}/leaves")]
    [Authorize(Policy = AuthPolicies.Staff)]
    public async Task<ActionResult<List<ResidentLeaveDto>>> Leaves(int residentId) =>
        Ok(await records.ListLeavesAsync(User.RequireCurrentUser(), residentId));

    [HttpPost("api/residents/{residentId:int}/leaves")]
    [Authorize(Policy = AuthPolicies.LeaveRecorders)]
    [Audit("Leave.Start", "Resident", "Ghi nhận cư dân tạm vắng", "residentId")]
    public async Task<ActionResult<ResidentLeaveDto>> StartLeave(int residentId, [FromBody] StartLeaveRequest request) =>
        Ok(await records.StartLeaveAsync(User.RequireCurrentUser(), residentId, request));

    [HttpPost("api/residents/{residentId:int}/leaves/{leaveId:int}/return")]
    [Authorize(Policy = AuthPolicies.LeaveRecorders)]
    [Audit("Leave.Return", "Resident", "Ghi nhận cư dân trở về", "residentId")]
    public async Task<ActionResult<ResidentLeaveDto>> ReturnFromLeave(int residentId, int leaveId, [FromBody] ReturnLeaveRequest request) =>
        Ok(await records.ReturnFromLeaveAsync(User.RequireCurrentUser(), residentId, leaveId, request));

    // ---------------- Lịch sử lưu trú, tái nhập viện ----------------

    [HttpGet("api/residents/{residentId:int}/stays")]
    [Authorize(Policy = AuthPolicies.FrontDesk)]
    public async Task<ActionResult<List<ResidentStayDto>>> Stays(int residentId) => Ok(await records.ListStaysAsync(residentId));

    [HttpPost("api/residents/{residentId:int}/readmit")]
    [Authorize(Policy = AuthPolicies.FrontDesk)]
    [Audit("Resident.Readmit", "Resident", "Tái nhập viện", "residentId")]
    public async Task<ActionResult<ResidentDetailDto>> Readmit(int residentId, [FromBody] ReadmitRequest request) =>
        Ok(await records.ReadmitAsync(User.RequireCurrentUser(), residentId, request));

    // ---------------- Tiền cọc: xem BillingStaff, thu/hoàn FinanceStaff ----------------

    [HttpGet("api/residents/{residentId:int}/deposits")]
    [Authorize(Policy = AuthPolicies.BillingStaff)]
    public async Task<ActionResult<DepositSummaryDto>> Deposits(int residentId) => Ok(await records.DepositsAsync(residentId));

    [HttpPost("api/residents/{residentId:int}/deposits")]
    [Authorize(Policy = AuthPolicies.FinanceStaff)]
    [Audit("Deposit.Create", "Resident", "Thu tiền cọc", "residentId")]
    public async Task<ActionResult<DepositSummaryDto>> CreateDeposit(int residentId, [FromBody] CreateDepositRequest request) =>
        Ok(await records.CreateDepositAsync(User.RequireCurrentUser(), residentId, request));

    [HttpPost("api/deposits/{depositId:int}/refund")]
    [Authorize(Policy = AuthPolicies.FinanceStaff)]
    [Audit("Deposit.Refund", "Deposit", "Hoàn/trừ tiền cọc", "depositId")]
    public async Task<ActionResult<DepositSummaryDto>> RefundDeposit(int depositId, [FromBody] RefundDepositRequest request) =>
        Ok(await records.RefundDepositAsync(User.RequireCurrentUser(), depositId, request));

    /// <summary>Chép file (≤ 10 MB) vào bộ nhớ để service đọc chữ ký rồi tua lại; quá cỡ thì trả stream rỗng để service báo lỗi rõ.</summary>
    private static async Task<Stream> ReadAsync(IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            throw new BusinessRuleException("Chưa chọn file.");
        }

        if (file.Length > ResidentRecordRules.MaxDocumentBytes)
        {
            return Stream.Null;
        }

        var memory = new MemoryStream((int)file.Length);
        await file.CopyToAsync(memory);
        memory.Position = 0;
        return memory;
    }

    /// <summary>Luôn tải về (attachment) — trình duyệt không mở file ngay trong trang của API.</summary>
    private FileStreamResult Download(StoredFile file) => File(file.Content, file.ContentType, file.FileName);
}
