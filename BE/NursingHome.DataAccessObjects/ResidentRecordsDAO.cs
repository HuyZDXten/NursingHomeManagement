using System.Data;
using Microsoft.EntityFrameworkCore;
using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Enums;
using NursingHome.BusinessObjects.Exceptions;

namespace NursingHome.DataAccessObjects;

/// <summary>
/// MODULE VẬN HÀNH (Viên) — BL-V2 hồ sơ lưu trú: giấy tờ (UC-29), hợp đồng (UC-30), tạm vắng (UC-31),
/// tái nhập viện (UC-33), tiền cọc (UC-99). Thao tác nhiều bước chạy trong một transaction Serializable.
/// </summary>
public sealed class ResidentRecordsDAO : GenericDAO<ResidentDocument>
{
    public static ResidentRecordsDAO Instance { get; } = new();

    private ResidentRecordsDAO()
    {
    }

    // ---------------- Giấy tờ ----------------

    public async Task<List<ResidentDocument>> ListDocumentsAsync(int residentId)
    {
        await using var context = CreateContext();
        return await context.ResidentDocuments.AsNoTracking().Include(item => item.UploadedBy)
            .Where(item => item.ResidentId == residentId).OrderByDescending(item => item.CreatedAt).ToListAsync();
    }

    public async Task<ResidentDocument?> GetDocumentAsync(int id)
    {
        await using var context = CreateContext();
        return await context.ResidentDocuments.AsNoTracking().Include(item => item.UploadedBy).FirstOrDefaultAsync(item => item.Id == id);
    }

    public Task<ResidentDocument> AddDocumentAsync(ResidentDocument document) => AddEntityAsync(document);

    public async Task<bool> DeleteDocumentAsync(int id)
    {
        await using var context = CreateContext();
        return await context.ResidentDocuments.Where(item => item.Id == id).ExecuteDeleteAsync() == 1;
    }

    // ---------------- Hợp đồng ----------------

    public async Task<List<ResidentContract>> ListContractsAsync(int residentId)
    {
        await using var context = CreateContext();
        return await context.ResidentContracts.AsNoTracking().Include(item => item.CreatedBy)
            .Where(item => item.ResidentId == residentId)
            .OrderBy(item => item.Status).ThenByDescending(item => item.StartDate).ToListAsync();
    }

    public async Task<ResidentContract?> GetContractAsync(int id)
    {
        await using var context = CreateContext();
        return await context.ResidentContracts.AsNoTracking().Include(item => item.CreatedBy).FirstOrDefaultAsync(item => item.Id == id);
    }

    /// <summary>Mỗi cư dân tối đa một hợp đồng Active (UX_ResidentContracts_Active chặn cả khi hai request song song).</summary>
    public async Task<ResidentContract> AddContractAsync(ResidentContract contract)
    {
        await using var context = CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        if (await context.ResidentContracts.AnyAsync(item => item.ResidentId == contract.ResidentId && item.Status == ContractStatus.Active))
        {
            throw new ConflictException("Cư dân đang có hợp đồng hiệu lực. Kết thúc hợp đồng cũ trước khi thêm hợp đồng mới.");
        }

        if (await context.ResidentContracts.AnyAsync(item => item.Number == contract.Number))
        {
            throw new ConflictException("Số hợp đồng đã tồn tại.");
        }

        context.ResidentContracts.Add(contract);
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
        return contract;
    }

    /// <summary>Kết thúc hợp đồng đang hiệu lực; EndDate giữ nguyên nếu đã có, không thì lấy ngày kết thúc truyền vào.</summary>
    public async Task<bool> EndContractAsync(int id, DateOnly endDate, DateTime now)
    {
        await using var context = CreateContext();
        var contract = await context.ResidentContracts.FirstOrDefaultAsync(item => item.Id == id && item.Status == ContractStatus.Active);
        if (contract is null)
        {
            return false;
        }

        contract.Status = ContractStatus.Ended;
        contract.EndDate ??= endDate < contract.StartDate ? contract.StartDate : endDate;
        contract.EndedAt = now;
        await context.SaveChangesAsync();
        return true;
    }

    // ---------------- Tạm vắng ----------------

    public async Task<List<ResidentLeave>> ListLeavesAsync(int residentId)
    {
        await using var context = CreateContext();
        return await context.ResidentLeaves.AsNoTracking().Include(item => item.CreatedBy)
            .Where(item => item.ResidentId == residentId).OrderByDescending(item => item.StartAt).ToListAsync();
    }

    /// <summary>Các lần vắng đang mở (để danh sách cư dân gắn nhãn "Tạm vắng").</summary>
    public async Task<List<ResidentLeave>> ListOpenLeavesAsync()
    {
        await using var context = CreateContext();
        return await context.ResidentLeaves.AsNoTracking().Include(item => item.Resident).Include(item => item.CreatedBy)
            .Where(item => item.ReturnedAt == null).OrderBy(item => item.StartAt).ToListAsync();
    }

    public async Task<ResidentLeave> StartLeaveAsync(ResidentLeave leave)
    {
        await using var context = CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var status = await context.Residents.Where(item => item.Id == leave.ResidentId).Select(item => (ResidentStatus?)item.Status).FirstOrDefaultAsync()
            ?? throw new NotFoundException("Không tìm thấy cư dân.");
        if (status != ResidentStatus.Active)
        {
            throw new BusinessRuleException("Cư dân đã ngừng lưu trú.");
        }

        if (await context.ResidentLeaves.AnyAsync(item => item.ResidentId == leave.ResidentId && item.ReturnedAt == null))
        {
            throw new ConflictException("Cư dân đang tạm vắng. Ghi nhận \"đã về\" trước khi tạo lần vắng mới.");
        }

        context.ResidentLeaves.Add(leave);
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
        return leave;
    }

    public async Task<bool> ReturnFromLeaveAsync(int leaveId, int residentId, int byAccountId, DateTime returnedAt)
    {
        await using var context = CreateContext();
        int changed = await context.ResidentLeaves
            .Where(item => item.Id == leaveId && item.ResidentId == residentId && item.ReturnedAt == null && item.StartAt <= returnedAt)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.ReturnedAt, returnedAt)
                .SetProperty(item => item.ReturnedById, byAccountId));
        return changed == 1;
    }

    // ---------------- Lịch sử lưu trú & tái nhập viện ----------------

    public async Task<List<ResidentStay>> ListStaysAsync(int residentId)
    {
        await using var context = CreateContext();
        return await context.ResidentStays.AsNoTracking().Where(item => item.ResidentId == residentId)
            .OrderByDescending(item => item.AdmittedAt).ToListAsync();
    }

    /// <summary>
    /// UC-33 trong MỘT transaction: lưu lần lưu trú cũ vào ResidentStays, đóng các gói dịch vụ còn mở tại ngày ngừng
    /// lưu trú (tránh tính tiền trùng), chuyển cư dân về Active, xếp giường trống, mở gói mới từ ngày bắt đầu.
    /// </summary>
    public async Task ReadmitAsync(int residentId, int bedId, int packageId, decimal? agreedPrice, DateOnly startDate,
        DateOnly dischargedOn, int byAccountId, DateTime now)
    {
        await using var context = CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var resident = await context.Residents.FindAsync(residentId) ?? throw new NotFoundException("Không tìm thấy cư dân.");
        if (resident.Status != ResidentStatus.Discharged || resident.DischargedAt is null)
        {
            throw new BusinessRuleException("Chỉ tái nhập viện cho cư dân đã ngừng lưu trú.");
        }

        var bed = await context.Beds.Include(item => item.Room).FirstOrDefaultAsync(item => item.Id == bedId)
            ?? throw new NotFoundException("Không tìm thấy giường.");
        if (bed.Status != BedStatus.Available || !bed.Room.IsActive
            || await context.BedAssignments.AnyAsync(item => item.BedId == bedId && item.EndedAt == null))
        {
            throw new BusinessRuleException("Giường không trống. Chọn giường khác.");
        }

        // BL-V3, UC-41: giường đang được giữ chỉ xếp cho đúng người đã giữ.
        await BedReservationGuard.EnsureAssignableAsync(context, bedId, residentId, now);

        var package = await context.ServicePackages.FindAsync(packageId);
        if (package is null || !package.IsActive)
        {
            throw new BusinessRuleException("Gói dịch vụ không tồn tại hoặc đã ngừng.");
        }

        context.ResidentStays.Add(new ResidentStay
        {
            ResidentId = residentId,
            AdmittedAt = resident.AdmittedAt,
            DischargedAt = resident.DischargedAt.Value,
            RecordedById = byAccountId,
            RecordedAt = now
        });

        var openSubscriptions = await context.ResidentSubscriptions
            .Where(item => item.ResidentId == residentId && (item.EndDate == null || item.EndDate > dischargedOn)).ToListAsync();
        foreach (var subscription in openSubscriptions)
        {
            subscription.EndDate = subscription.StartDate > dischargedOn ? subscription.StartDate : dischargedOn;
            subscription.UpdatedAt = now;
        }

        resident.Status = ResidentStatus.Active;
        resident.AdmittedAt = now;
        resident.DischargedAt = null;
        resident.UpdatedAt = now;

        context.BedAssignments.Add(new BedAssignment { ResidentId = residentId, BedId = bedId, StartedAt = now, AssignedById = byAccountId, CreatedAt = now });
        bed.Status = BedStatus.Occupied;
        bed.UpdatedAt = now;

        context.ResidentSubscriptions.Add(new ResidentSubscription
        {
            ResidentId = residentId,
            ServicePackageId = packageId,
            StartDate = startDate,
            AgreedMonthlyPrice = agreedPrice ?? package.MonthlyPrice,
            CreatedAt = now
        });

        await context.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    // ---------------- Tiền cọc ----------------

    public async Task<List<Deposit>> ListDepositsAsync(int residentId)
    {
        await using var context = CreateContext();
        return await context.Deposits.AsNoTracking().Include(item => item.ReceivedBy).Include(item => item.RefundedBy)
            .Where(item => item.ResidentId == residentId).OrderByDescending(item => item.ReceivedAt).ToListAsync();
    }

    public async Task<Deposit?> GetDepositAsync(int id)
    {
        await using var context = CreateContext();
        return await context.Deposits.AsNoTracking().Include(item => item.Resident).Include(item => item.ReceivedBy).Include(item => item.RefundedBy)
            .FirstOrDefaultAsync(item => item.Id == id);
    }

    public async Task<Deposit> AddDepositAsync(Deposit deposit)
    {
        await using var context = CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var status = await context.Residents.Where(item => item.Id == deposit.ResidentId).Select(item => (ResidentStatus?)item.Status).FirstOrDefaultAsync()
            ?? throw new NotFoundException("Không tìm thấy cư dân.");
        if (status != ResidentStatus.Active)
        {
            throw new BusinessRuleException("Cư dân đã ngừng lưu trú, không thu cọc.");
        }

        if (await context.Deposits.AnyAsync(item => item.ResidentId == deposit.ResidentId && item.RefundedAt == null))
        {
            throw new ConflictException("Cư dân đang có khoản cọc chưa hoàn.");
        }

        context.Deposits.Add(deposit);
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
        return deposit;
    }

    /// <summary>Hoàn cọc: chỉ khi cư dân đã ngừng lưu trú, số hoàn ≤ số cọc; một khoản chỉ hoàn một lần.</summary>
    public async Task RefundDepositAsync(int id, decimal amount, string? note, int byAccountId, DateTime now)
    {
        await using var context = CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var deposit = await context.Deposits.Include(item => item.Resident).FirstOrDefaultAsync(item => item.Id == id)
            ?? throw new NotFoundException("Không tìm thấy khoản cọc.");
        if (deposit.RefundedAt is not null)
        {
            throw new BusinessRuleException("Khoản cọc đã được hoàn.");
        }

        if (deposit.Resident.Status != ResidentStatus.Discharged)
        {
            throw new BusinessRuleException("Chỉ hoàn cọc sau khi cư dân ngừng lưu trú.");
        }

        if (amount > deposit.Amount)
        {
            throw new BusinessRuleException("Số tiền hoàn không được lớn hơn số tiền cọc.");
        }

        deposit.RefundedAt = now;
        deposit.RefundAmount = amount;
        deposit.RefundNote = note;
        deposit.RefundedById = byAccountId;
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    /// <summary>Công nợ hiện tại của cư dân: tổng (thành tiền − đã trả) của hóa đơn chưa trả xong.</summary>
    public async Task<decimal> OutstandingAsync(int residentId)
    {
        await using var context = CreateContext();
        var open = await context.Invoices.AsNoTracking()
            .Where(item => item.ResidentId == residentId && (item.Status == InvoiceStatus.Unpaid || item.Status == InvoiceStatus.PartiallyPaid))
            .Select(item => new
            {
                Total = item.Items.Sum(line => line.Quantity * line.UnitPrice),
                Paid = item.Payments.Sum(payment => payment.Amount)
            })
            .ToListAsync();
        return open.Sum(item => Math.Max(0, Math.Round(item.Total, 0) - item.Paid));
    }
}
