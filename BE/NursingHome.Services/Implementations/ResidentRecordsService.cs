using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Enums;
using NursingHome.BusinessObjects.Exceptions;
using NursingHome.DTOs.Common;
using NursingHome.DTOs.Residents;
using NursingHome.Repositories.Interfaces;
using NursingHome.Services.Helpers;
using NursingHome.Services.Interfaces;

namespace NursingHome.Services.Implementations;

/// <summary>
/// BL-V2 hồ sơ lưu trú. Giấy tờ (CCCD, BHYT...) chỉ lễ tân/Admin (policy ở controller). Hợp đồng theo quyền phần Billing
/// của cư dân (kế toán xem được, gia đình có quyền hóa đơn xem/tải được). Tạm vắng chỉ đánh dấu, không tự hủy việc
/// chăm sóc hay lịch thuốc (phần của Huy) và không đổi cách tính hóa đơn.
/// </summary>
public class ResidentRecordsService(
    IResidentRecordsRepository records,
    IResidentRepository residents,
    IResidentAccessService access,
    IResidentService residentService,
    IDocumentStorage storage,
    FacilityClock clock) : IResidentRecordsService
{
    // ---------------- Giấy tờ (UC-29) ----------------

    public async Task<List<ResidentDocumentDto>> ListDocumentsAsync(int residentId)
    {
        await EnsureResidentAsync(residentId);
        return (await records.ListDocumentsAsync(residentId)).Select(item => item.ToDto()).ToList();
    }

    public async Task<ResidentDocumentDto> UploadDocumentAsync(CurrentUser user, int residentId, UploadDocumentRequest request, Stream content, string? fileName, long length)
    {
        await EnsureResidentAsync(residentId);
        var (stored, contentType) = await SaveFileAsync(content, fileName, length);
        var document = await records.AddDocumentAsync(new ResidentDocument
        {
            ResidentId = residentId,
            Kind = Enum.Parse<DocumentKind>(request.Kind),
            Title = request.Title.Trim(),
            FileName = SafeFileName(fileName),
            StoredName = stored,
            ContentType = contentType,
            SizeBytes = length,
            UploadedById = user.AccountId,
            CreatedAt = clock.UtcNow
        });
        return (await records.GetDocumentAsync(document.Id))!.ToDto();
    }

    public async Task<StoredFile> OpenDocumentAsync(int residentId, int documentId)
    {
        var document = await records.GetDocumentAsync(documentId);
        if (document is null || document.ResidentId != residentId)
        {
            throw new NotFoundException("Không tìm thấy giấy tờ.");
        }

        return new StoredFile(Open(document.StoredName), document.ContentType, document.FileName);
    }

    public async Task DeleteDocumentAsync(int residentId, int documentId)
    {
        var document = await records.GetDocumentAsync(documentId);
        if (document is null || document.ResidentId != residentId)
        {
            throw new NotFoundException("Không tìm thấy giấy tờ.");
        }

        if (await records.DeleteDocumentAsync(documentId))
        {
            storage.Delete(document.StoredName);
        }
    }

    // ---------------- Hợp đồng (UC-30) ----------------

    public async Task<List<ResidentContractDto>> ListContractsAsync(CurrentUser user, int residentId)
    {
        await access.RequireAsync(user, residentId, ResidentSection.Billing);
        return (await records.ListContractsAsync(residentId)).Select(item => item.ToDto()).ToList();
    }

    public async Task<ResidentContractDto> CreateContractAsync(CurrentUser user, int residentId, CreateContractRequest request, Stream? content, string? fileName, long length)
    {
        var resident = await EnsureResidentAsync(residentId);
        if (resident.Status != ResidentStatus.Active)
        {
            throw new BusinessRuleException("Cư dân đã ngừng lưu trú.");
        }

        ResidentRecordRules.ValidateContract(request);
        string? stored = null;
        string? contentType = null;
        if (content is not null)
        {
            (stored, contentType) = await SaveFileAsync(content, fileName, length);
        }

        try
        {
            var contract = await records.AddContractAsync(new ResidentContract
            {
                ResidentId = residentId,
                Number = request.Number.Trim(),
                SignedOn = request.SignedOn,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                MonthlyPrice = request.MonthlyPrice,
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                FileName = stored is null ? null : SafeFileName(fileName),
                StoredName = stored,
                ContentType = contentType,
                SizeBytes = stored is null ? null : length,
                Status = ContractStatus.Active,
                CreatedById = user.AccountId,
                CreatedAt = clock.UtcNow
            });
            return (await records.GetContractAsync(contract.Id))!.ToDto();
        }
        catch
        {
            if (stored is not null)
            {
                storage.Delete(stored); // không để file mồ côi khi trùng số hợp đồng / đã có hợp đồng hiệu lực
            }

            throw;
        }
    }

    public async Task<ResidentContractDto> EndContractAsync(int residentId, int contractId, EndContractRequest request)
    {
        var contract = await records.GetContractAsync(contractId);
        if (contract is null || contract.ResidentId != residentId)
        {
            throw new NotFoundException("Không tìm thấy hợp đồng.");
        }

        if (!await records.EndContractAsync(contractId, request.EndDate ?? clock.Today, clock.UtcNow))
        {
            throw new BusinessRuleException("Hợp đồng đã kết thúc.");
        }

        return (await records.GetContractAsync(contractId))!.ToDto();
    }

    public async Task<StoredFile> OpenContractFileAsync(CurrentUser user, int residentId, int contractId)
    {
        await access.RequireAsync(user, residentId, ResidentSection.Billing);
        var contract = await records.GetContractAsync(contractId);
        if (contract is null || contract.ResidentId != residentId || contract.StoredName is null)
        {
            throw new NotFoundException("Hợp đồng chưa có file đính kèm.");
        }

        return new StoredFile(Open(contract.StoredName), contract.ContentType ?? "application/octet-stream", contract.FileName ?? "hop-dong");
    }

    // ---------------- Tạm vắng (UC-31) ----------------

    public async Task<List<ResidentLeaveDto>> ListLeavesAsync(CurrentUser user, int residentId)
    {
        await access.RequireAsync(user, residentId, ResidentSection.Basic);
        var today = clock.Today;
        return (await records.ListLeavesAsync(residentId)).Select(item => item.ToDto(today)).ToList();
    }

    public async Task<List<ResidentLeaveDto>> OpenLeavesAsync(CurrentUser user)
    {
        var visible = await access.VisibleResidentIdsAsync(user);
        var today = clock.Today;
        return (await records.ListOpenLeavesAsync())
            .Where(item => visible is null || visible.Contains(item.ResidentId))
            .Select(item => item.ToDto(today)).ToList();
    }

    public async Task<ResidentLeaveDto> StartLeaveAsync(CurrentUser user, int residentId, StartLeaveRequest request)
    {
        var now = clock.UtcNow;
        var start = request.StartAt?.ToUniversalTime() ?? now;
        ResidentRecordRules.ValidateLeave(start, request.ExpectedReturnOn, now);
        var leave = await records.StartLeaveAsync(new ResidentLeave
        {
            ResidentId = residentId,
            Kind = Enum.Parse<LeaveKind>(request.Kind),
            StartAt = start,
            ExpectedReturnOn = request.ExpectedReturnOn,
            Reason = request.Reason.Trim(),
            CreatedById = user.AccountId,
            CreatedAt = now
        });
        return await LeaveDtoAsync(residentId, leave.Id);
    }

    public async Task<ResidentLeaveDto> ReturnFromLeaveAsync(CurrentUser user, int residentId, int leaveId, ReturnLeaveRequest request)
    {
        var leave = (await records.ListLeavesAsync(residentId)).FirstOrDefault(item => item.Id == leaveId)
            ?? throw new NotFoundException("Không tìm thấy lần tạm vắng.");
        var now = clock.UtcNow;
        var returnedAt = request.ReturnedAt?.ToUniversalTime() ?? now;
        ResidentRecordRules.ValidateReturn(leave.StartAt, returnedAt, now);
        if (!await records.ReturnFromLeaveAsync(leaveId, residentId, user.AccountId, returnedAt))
        {
            throw new BusinessRuleException("Cư dân đã được ghi nhận trở về.");
        }

        return await LeaveDtoAsync(residentId, leaveId);
    }

    // ---------------- Lịch sử lưu trú, tái nhập viện (UC-33) ----------------

    public async Task<List<ResidentStayDto>> ListStaysAsync(int residentId)
    {
        await EnsureResidentAsync(residentId);
        return (await records.ListStaysAsync(residentId)).Select(item => item.ToDto()).ToList();
    }

    public async Task<ResidentDetailDto> ReadmitAsync(CurrentUser user, int residentId, ReadmitRequest request)
    {
        var resident = await EnsureResidentAsync(residentId);
        if (resident.Status != ResidentStatus.Discharged || resident.DischargedAt is null)
        {
            throw new BusinessRuleException("Chỉ tái nhập viện cho cư dân đã ngừng lưu trú.");
        }

        if (request.AgreedMonthlyPrice is { } price)
        {
            ResidentRecordRules.EnsureWholeVnd(price, "Giá thỏa thuận");
        }

        var dischargedOn = FacilityClock.ToLocalDate(resident.DischargedAt.Value);
        ResidentRecordRules.ValidateReadmitStart(request.StartDate, dischargedOn, clock.Today);
        await records.ReadmitAsync(residentId, request.BedId, request.ServicePackageId, request.AgreedMonthlyPrice,
            request.StartDate, dischargedOn, user.AccountId, clock.UtcNow);
        return await residentService.GetAsync(user, residentId);
    }

    // ---------------- Tiền cọc (UC-99) ----------------

    public async Task<DepositSummaryDto> DepositsAsync(int residentId)
    {
        var resident = await EnsureResidentAsync(residentId);
        var deposits = await records.ListDepositsAsync(residentId);
        return new DepositSummaryDto
        {
            ResidentStatus = resident.Status.ToString(),
            HeldAmount = deposits.Where(item => item.RefundedAt is null).Sum(item => item.Amount),
            Outstanding = await records.OutstandingAsync(residentId),
            Deposits = deposits.Select(item => item.ToDto()).ToList()
        };
    }

    public async Task<DepositSummaryDto> CreateDepositAsync(CurrentUser user, int residentId, CreateDepositRequest request)
    {
        ResidentRecordRules.EnsureWholeVnd(request.Amount, "Số tiền cọc");
        await records.AddDepositAsync(new Deposit
        {
            ResidentId = residentId,
            Amount = request.Amount,
            Method = Enum.Parse<DepositMethod>(request.Method),
            Reference = string.IsNullOrWhiteSpace(request.Reference) ? null : request.Reference.Trim(),
            ReceivedAt = clock.UtcNow,
            ReceivedById = user.AccountId
        });
        return await DepositsAsync(residentId);
    }

    public async Task<DepositSummaryDto> RefundDepositAsync(CurrentUser user, int depositId, RefundDepositRequest request)
    {
        ResidentRecordRules.EnsureWholeVnd(request.Amount, "Số tiền hoàn");
        var deposit = await records.GetDepositAsync(depositId) ?? throw new NotFoundException("Không tìm thấy khoản cọc.");
        await records.RefundDepositAsync(depositId, request.Amount,
            string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(), user.AccountId, clock.UtcNow);
        return await DepositsAsync(deposit.ResidentId);
    }

    // ---------------- Hỗ trợ ----------------

    private async Task<Resident> EnsureResidentAsync(int residentId) =>
        await residents.GetByIdAsync(residentId) ?? throw new NotFoundException("Không tìm thấy cư dân.");

    private async Task<ResidentLeaveDto> LeaveDtoAsync(int residentId, int leaveId)
    {
        var resident = await EnsureResidentAsync(residentId);
        var leave = (await records.ListLeavesAsync(residentId)).First(item => item.Id == leaveId);
        leave.Resident = resident;
        return leave.ToDto(clock.Today);
    }

    /// <summary>Kiểm chữ ký file rồi lưu riêng tư; stream phải tua lại được (controller chép vào MemoryStream).</summary>
    private async Task<(string StoredName, string ContentType)> SaveFileAsync(Stream content, string? fileName, long length)
    {
        if (length <= 0 || length > ResidentRecordRules.MaxDocumentBytes)
        {
            ResidentRecordRules.ValidateDocument(fileName, length, ReadOnlySpan<byte>.Empty);
        }

        var header = new byte[ImageRules.HeaderLength];
        int read = await content.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false);
        var (extension, contentType) = ResidentRecordRules.ValidateDocument(fileName, length, header.AsSpan(0, read));
        content.Position = 0;
        return (await storage.SaveAsync(content, extension), contentType);
    }

    private Stream Open(string storedName)
    {
        try
        {
            return storage.OpenRead(storedName);
        }
        catch (FileNotFoundException)
        {
            throw new NotFoundException("File không còn trên máy chủ.");
        }
    }

    /// <summary>Chỉ giữ tên file (bỏ đường dẫn), cắt 200 ký tự — dùng cho tiêu đề tải về.</summary>
    private static string SafeFileName(string? fileName)
    {
        string name = Path.GetFileName(fileName ?? "").Trim();
        if (name.Length == 0)
        {
            name = "tep";
        }

        return name.Length > 200 ? name[^200..] : name;
    }
}
