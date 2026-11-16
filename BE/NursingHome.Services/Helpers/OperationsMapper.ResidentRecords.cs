using NursingHome.BusinessObjects.Entities;
using NursingHome.DTOs.Residents;

namespace NursingHome.Services.Helpers;

public static partial class OperationsMapper
{
    public static ResidentDocumentDto ToDto(this ResidentDocument document) => new()
    {
        Id = document.Id,
        ResidentId = document.ResidentId,
        Kind = document.Kind.ToString(),
        Title = document.Title,
        FileName = document.FileName,
        ContentType = document.ContentType,
        SizeBytes = document.SizeBytes,
        UploadedByName = document.UploadedBy?.FullName,
        CreatedAt = document.CreatedAt
    };

    public static ResidentContractDto ToDto(this ResidentContract contract) => new()
    {
        Id = contract.Id,
        ResidentId = contract.ResidentId,
        Number = contract.Number,
        SignedOn = contract.SignedOn,
        StartDate = contract.StartDate,
        EndDate = contract.EndDate,
        MonthlyPrice = contract.MonthlyPrice,
        Notes = contract.Notes,
        HasFile = contract.StoredName is not null,
        FileName = contract.FileName,
        Status = contract.Status.ToString(),
        CreatedByName = contract.CreatedBy?.FullName,
        CreatedAt = contract.CreatedAt,
        EndedAt = contract.EndedAt
    };

    public static ResidentLeaveDto ToDto(this ResidentLeave leave, DateOnly today) => new()
    {
        Id = leave.Id,
        ResidentId = leave.ResidentId,
        ResidentCode = leave.Resident?.Code ?? "",
        ResidentName = leave.Resident?.FullName ?? "",
        Kind = leave.Kind.ToString(),
        StartAt = leave.StartAt,
        ExpectedReturnOn = leave.ExpectedReturnOn,
        ReturnedAt = leave.ReturnedAt,
        Reason = leave.Reason,
        CreatedByName = leave.CreatedBy?.FullName,
        IsOpen = leave.ReturnedAt is null,
        Overdue = leave.ReturnedAt is null && leave.ExpectedReturnOn is { } back && back < today
    };

    public static ResidentStayDto ToDto(this ResidentStay stay) => new()
    {
        Id = stay.Id,
        AdmittedAt = stay.AdmittedAt,
        DischargedAt = stay.DischargedAt
    };

    public static DepositDto ToDto(this Deposit deposit) => new()
    {
        Id = deposit.Id,
        ResidentId = deposit.ResidentId,
        Amount = deposit.Amount,
        Method = deposit.Method.ToString(),
        Reference = deposit.Reference,
        ReceivedAt = deposit.ReceivedAt,
        ReceivedByName = deposit.ReceivedBy?.FullName,
        RefundedAt = deposit.RefundedAt,
        RefundAmount = deposit.RefundAmount,
        RefundNote = deposit.RefundNote,
        RefundedByName = deposit.RefundedBy?.FullName,
        Held = deposit.RefundedAt is null
    };
}
