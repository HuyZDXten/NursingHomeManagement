namespace NursingHome.BusinessObjects.Entities;

// MODULE VẬN HÀNH (Viên) — BL-V2 hồ sơ lưu trú (schema mục 9.8).

public enum DocumentKind
{
    IdCard,
    Insurance,
    Medical,
    Other
}

public enum ContractStatus
{
    Active,
    Ended
}

public enum LeaveKind
{
    Home,
    Hospital,
    Other
}

public enum DepositMethod
{
    Cash,
    BankTransfer
}

/// <summary>UC-29: giấy tờ (CCCD, thẻ BHYT, giấy khám...). File lưu riêng tư, StoredName = GUID + đuôi.</summary>
public class ResidentDocument
{
    public int Id { get; set; }

    public int ResidentId { get; set; }

    public DocumentKind Kind { get; set; }

    public string Title { get; set; } = "";

    public string FileName { get; set; } = "";

    public string StoredName { get; set; } = "";

    public string ContentType { get; set; } = "";

    public long SizeBytes { get; set; }

    public int UploadedById { get; set; }

    public Account UploadedBy { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
}

/// <summary>UC-30: hợp đồng lưu trú. Mỗi cư dân tối đa một hợp đồng Active; file đính kèm không bắt buộc.</summary>
public class ResidentContract
{
    public int Id { get; set; }

    public int ResidentId { get; set; }

    public string Number { get; set; } = "";

    public DateOnly SignedOn { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public decimal? MonthlyPrice { get; set; }

    public string? Notes { get; set; }

    public string? FileName { get; set; }

    public string? StoredName { get; set; }

    public string? ContentType { get; set; }

    public long? SizeBytes { get; set; }

    public ContractStatus Status { get; set; } = ContractStatus.Active;

    public int CreatedById { get; set; }

    public Account CreatedBy { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? EndedAt { get; set; }
}

/// <summary>UC-31: tạm vắng (về nhà, nằm viện). ReturnedAt null = đang vắng; tối đa một lần vắng đang mở.</summary>
public class ResidentLeave
{
    public int Id { get; set; }

    public int ResidentId { get; set; }

    public Resident Resident { get; set; } = null!;

    public LeaveKind Kind { get; set; }

    public DateTime StartAt { get; set; }

    public DateOnly? ExpectedReturnOn { get; set; }

    public DateTime? ReturnedAt { get; set; }

    public string Reason { get; set; } = "";

    public int CreatedById { get; set; }

    public Account CreatedBy { get; set; } = null!;

    public int? ReturnedById { get; set; }

    public DateTime CreatedAt { get; set; }
}

/// <summary>UC-33: một lần lưu trú đã kết thúc, ghi lại lúc tái nhập viện.</summary>
public class ResidentStay
{
    public int Id { get; set; }

    public int ResidentId { get; set; }

    public DateTime AdmittedAt { get; set; }

    public DateTime DischargedAt { get; set; }

    public int RecordedById { get; set; }

    public DateTime RecordedAt { get; set; }
}

/// <summary>UC-99: tiền cọc. RefundedAt null = đang giữ; tối đa một khoản đang giữ mỗi cư dân.</summary>
public class Deposit
{
    public int Id { get; set; }

    public int ResidentId { get; set; }

    public Resident Resident { get; set; } = null!;

    public decimal Amount { get; set; }

    public DepositMethod Method { get; set; }

    public string? Reference { get; set; }

    public DateTime ReceivedAt { get; set; }

    public int ReceivedById { get; set; }

    public Account ReceivedBy { get; set; } = null!;

    public DateTime? RefundedAt { get; set; }

    public decimal? RefundAmount { get; set; }

    public string? RefundNote { get; set; }

    public int? RefundedById { get; set; }

    public Account? RefundedBy { get; set; }
}
