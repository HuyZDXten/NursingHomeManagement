using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Helpers;
using NursingHome.DTOs.Billing;

namespace NursingHome.Services.Helpers;

/// <summary>S3-V5 dịch vụ phát sinh &amp; nhắc nợ.</summary>
public static partial class OperationsMapper
{
    public static ExtraServiceDto ToDto(this ExtraService service) => new()
    {
        Id = service.Id,
        Name = service.Name,
        Unit = service.Unit,
        UnitPrice = service.UnitPrice,
        IsActive = service.IsActive
    };

    /// <summary>Cần Include Resident, ExtraService, RecordedBy, InvoiceItem.</summary>
    public static ServiceUsageDto ToDto(this ServiceUsage usage) => new()
    {
        Id = usage.Id,
        ResidentId = usage.ResidentId,
        ResidentCode = usage.Resident.Code,
        ResidentName = usage.Resident.FullName,
        ExtraServiceId = usage.ExtraServiceId,
        ServiceName = usage.ExtraService.Name,
        Unit = usage.ExtraService.Unit,
        UsedOn = usage.UsedOn,
        Quantity = usage.Quantity,
        UnitPrice = usage.UnitPrice,
        Amount = InvoiceMath.LineTotal(new InvoiceItem { Quantity = usage.Quantity, UnitPrice = usage.UnitPrice }),
        Note = usage.Note,
        RecordedById = usage.RecordedById,
        RecordedByName = usage.RecordedBy.FullName,
        Invoiced = usage.InvoiceItemId is not null,
        InvoiceId = usage.InvoiceItem?.InvoiceId,
        CreatedAt = usage.CreatedAt
    };

    public static InvoiceReminderDto ToDto(this InvoiceReminder reminder) => new()
    {
        Stage = reminder.Stage.ToString(),
        SentAt = reminder.SentAt
    };

    public static string ToVietnamese(this ReminderStage stage) => stage switch
    {
        ReminderStage.Before3Days => "sắp đến hạn",
        ReminderStage.Overdue => "đã quá hạn",
        _ => "quá hạn trên 7 ngày"
    };
}
