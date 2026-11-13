using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Helpers;
using NursingHome.DTOs.Accounts;
using NursingHome.DTOs.Auth;
using NursingHome.DTOs.Billing;
using NursingHome.DTOs.Care;
using NursingHome.DTOs.Medication;
using NursingHome.DTOs.Payments;
using NursingHome.DTOs.Portal;
using NursingHome.DTOs.Residents;
using NursingHome.DTOs.Rooms;

namespace NursingHome.Services.Helpers;

/// <summary>Entity → DTO. Không bao giờ trả entity ra API (tránh lộ PasswordHash, vòng tham chiếu).</summary>
public static class DtoMapper
{
    public static AccountDto ToDto(this Account account) => new()
    {
        Id = account.Id,
        Email = account.Email,
        FullName = account.FullName,
        Phone = account.Phone,
        Role = account.Role,
        IsActive = account.IsActive,
        CreatedAt = account.CreatedAt
    };

    public static ProfileDto ToProfile(this Account account) => new()
    {
        Id = account.Id,
        Email = account.Email,
        FullName = account.FullName,
        Phone = account.Phone,
        Role = account.Role,
        AvatarUrl = account.AvatarUrl
    };

    public static string? BedLabel(this Resident resident)
    {
        var current = resident.BedAssignments.FirstOrDefault(assignment => assignment.EndedAt == null);
        return current?.Bed?.Room is null ? null : $"Phòng {current.Bed.Room.Number} - Giường {current.Bed.Number}";
    }

    public static int AgeOn(DateOnly dateOfBirth, DateOnly today)
    {
        int age = today.Year - dateOfBirth.Year;
        return dateOfBirth > today.AddYears(-age) ? age - 1 : age;
    }

    public static ResidentListItemDto ToListItem(this Resident resident, DateOnly today) => Fill(new ResidentListItemDto(), resident, today);

    public static ResidentDetailDto ToDetail(this Resident resident, DateOnly today)
    {
        var dto = Fill(new ResidentDetailDto(), resident, today);
        dto.Notes = resident.Notes;
        dto.PhotoUrl = resident.PhotoUrl;
        return dto;
    }

    private static T Fill<T>(T dto, Resident resident, DateOnly today) where T : ResidentListItemDto
    {
        dto.Id = resident.Id;
        dto.Code = resident.Code;
        dto.FullName = resident.FullName;
        dto.DateOfBirth = resident.DateOfBirth;
        dto.Age = AgeOn(resident.DateOfBirth, today);
        dto.Gender = resident.Gender;
        dto.Status = resident.Status.ToString();
        dto.AdmittedAt = resident.AdmittedAt;
        dto.DischargedAt = resident.DischargedAt;
        dto.BedId = resident.BedAssignments.FirstOrDefault(assignment => assignment.EndedAt == null)?.BedId;
        dto.BedLabel = resident.BedLabel();
        return dto;
    }

    public static ContactDto ToDto(this FamilyContact contact) => new()
    {
        Id = contact.Id,
        ResidentId = contact.ResidentId,
        FullName = contact.FullName,
        Relationship = contact.Relationship,
        Phone = contact.Phone,
        Email = contact.Email,
        IsEmergencyContact = contact.IsEmergencyContact
    };

    public static FamilyLinkDto ToDto(this ResidentFamilyLink link) => new()
    {
        Id = link.Id,
        ResidentId = link.ResidentId,
        FamilyAccountId = link.FamilyAccountId,
        FamilyName = link.FamilyAccount?.FullName ?? "",
        FamilyEmail = link.FamilyAccount?.Email ?? "",
        CanViewHealth = link.CanViewHealth,
        CanViewCare = link.CanViewCare,
        CanViewMedication = link.CanViewMedication,
        CanViewBilling = link.CanViewBilling
    };

    public static RoomDto ToDto(this Room room) => new()
    {
        Id = room.Id,
        Number = room.Number,
        Floor = room.Floor,
        Description = room.Description,
        IsActive = room.IsActive,
        Beds = room.Beds.OrderBy(bed => bed.Number).Select(ToDto).ToList()
    };

    public static BedDto ToDto(this Bed bed)
    {
        var occupant = bed.Assignments.FirstOrDefault(assignment => assignment.EndedAt == null)?.Resident;
        return new BedDto
        {
            Id = bed.Id,
            RoomId = bed.RoomId,
            Number = bed.Number,
            Status = bed.Status.ToString(),
            ResidentId = occupant?.Id,
            ResidentName = occupant?.FullName,
            ResidentCode = occupant?.Code
        };
    }

    public static BedAssignmentDto ToDto(this BedAssignment assignment) => new()
    {
        Id = assignment.Id,
        ResidentId = assignment.ResidentId,
        ResidentName = assignment.Resident?.FullName ?? "",
        RoomNumber = assignment.Bed?.Room?.Number ?? "",
        BedNumber = assignment.Bed?.Number ?? "",
        StartedAt = assignment.StartedAt,
        EndedAt = assignment.EndedAt,
        AssignedByName = assignment.AssignedBy?.FullName ?? ""
    };

    public static CareTaskDto ToDto(this CareTask task) => new()
    {
        Id = task.Id,
        ResidentId = task.ResidentId,
        ResidentName = task.Resident?.FullName ?? "",
        ResidentCode = task.Resident?.Code ?? "",
        Title = task.Title,
        Instructions = task.Instructions,
        AssignedToId = task.AssignedToId,
        AssignedToName = task.AssignedTo?.FullName ?? "",
        DueAt = task.DueAt,
        Status = task.Status.ToString(),
        RowVersion = Convert.ToBase64String(task.RowVersion)
    };

    public static CareRecordDto ToDto(this CareRecord record) => new()
    {
        Id = record.Id,
        CareTaskId = record.CareTaskId,
        TaskTitle = record.CareTask?.Title ?? "",
        Status = record.Status.ToString(),
        Result = record.Result,
        PerformedAt = record.PerformedAt,
        RecordedByName = record.RecordedBy?.FullName ?? "",
        ShareWithFamily = record.ShareWithFamily
    };

    public static VitalSignDto ToDto(this VitalSign vital) => new()
    {
        Id = vital.Id,
        ResidentId = vital.ResidentId,
        Systolic = vital.Systolic,
        Diastolic = vital.Diastolic,
        HeartRate = vital.HeartRate,
        Temperature = vital.Temperature,
        SpO2 = vital.SpO2,
        Notes = vital.Notes,
        RecordedAt = vital.RecordedAt,
        RecordedByName = vital.RecordedBy?.FullName ?? "",
        ShareWithFamily = vital.ShareWithFamily
    };

    public static MedicationScheduleDto ToDto(this MedicationSchedule schedule) => new()
    {
        Id = schedule.Id,
        ResidentId = schedule.ResidentId,
        ResidentName = schedule.Resident?.FullName ?? "",
        MedicationName = schedule.MedicationName,
        Dose = schedule.Dose,
        Route = schedule.Route,
        StartDate = schedule.StartDate,
        EndDate = schedule.EndDate,
        ScheduledTime = schedule.ScheduledTime,
        ApprovalReference = schedule.ApprovalReference,
        Instructions = schedule.Instructions,
        EnteredByName = schedule.EnteredBy?.FullName ?? "",
        IsActive = schedule.IsActive
    };

    public static MedicationLogDto ToDto(this MedicationLog log) => new()
    {
        Id = log.Id,
        ScheduleId = log.MedicationScheduleId,
        MedicationName = log.MedicationSchedule?.MedicationName ?? "",
        Dose = log.MedicationSchedule?.Dose ?? "",
        ScheduledAt = log.ScheduledAt,
        RecordedAt = log.RecordedAt,
        Status = log.Status.ToString(),
        RecordedByName = log.RecordedBy?.FullName,
        Notes = log.Notes
    };

    public static ServicePackageDto ToDto(this ServicePackage package) => new()
    {
        Id = package.Id,
        Name = package.Name,
        MonthlyPrice = package.MonthlyPrice,
        Description = package.Description,
        IsActive = package.IsActive
    };

    public static SubscriptionDto ToDto(this ResidentSubscription subscription) => new()
    {
        Id = subscription.Id,
        ResidentId = subscription.ResidentId,
        ResidentName = subscription.Resident?.FullName ?? "",
        ServicePackageId = subscription.ServicePackageId,
        PackageName = subscription.ServicePackage?.Name ?? "",
        StartDate = subscription.StartDate,
        EndDate = subscription.EndDate,
        AgreedMonthlyPrice = subscription.AgreedMonthlyPrice
    };

    public static InvoiceListItemDto ToListItem(this Invoice invoice) => FillInvoice(new InvoiceListItemDto(), invoice);

    public static InvoiceDetailDto ToDetail(this Invoice invoice)
    {
        var dto = FillInvoice(new InvoiceDetailDto(), invoice);
        dto.Items = invoice.Items.OrderBy(item => item.Id).Select(item => new InvoiceItemDto
        {
            Id = item.Id,
            Description = item.Description,
            Quantity = item.Quantity,
            UnitPrice = item.UnitPrice,
            Amount = InvoiceMath.LineTotal(item)
        }).ToList();
        dto.Payments = invoice.Payments.OrderBy(payment => payment.PaidAt).Select(payment => new PaymentDto
        {
            Id = payment.Id,
            Amount = payment.Amount,
            Method = payment.Method.ToString(),
            PaidAt = payment.PaidAt,
            Reference = payment.Reference
        }).ToList();
        return dto;
    }

    private static T FillInvoice<T>(T dto, Invoice invoice) where T : InvoiceListItemDto
    {
        decimal total = InvoiceMath.Total(invoice.Items);
        decimal paid = InvoiceMath.Paid(invoice.Payments);
        dto.Id = invoice.Id;
        dto.Number = invoice.Number;
        dto.ResidentId = invoice.ResidentId;
        dto.ResidentName = invoice.Resident?.FullName ?? "";
        dto.ResidentCode = invoice.Resident?.Code ?? "";
        dto.BillingPeriod = invoice.BillingPeriod;
        dto.DueDate = invoice.DueDate;
        dto.IssuedAt = invoice.IssuedAt;
        dto.Status = invoice.Status.ToString();
        dto.Total = total;
        dto.Paid = paid;
        dto.Remaining = total - paid;
        return dto;
    }

    public static PaymentTransactionDto ToDto(this PaymentTransaction transaction) => new()
    {
        Id = transaction.Id,
        InvoiceId = transaction.InvoiceId,
        InvoiceNumber = transaction.Invoice?.Number ?? "",
        ResidentName = transaction.Invoice?.Resident?.FullName ?? "",
        Amount = transaction.Amount,
        Status = transaction.Status.ToString(),
        OrderCode = transaction.OrderCode,
        Attempt = transaction.Attempt,
        CreatedAt = transaction.CreatedAt,
        ExpiresAt = transaction.ExpiresAt,
        PaidAt = transaction.PaidAt
    };

    public static WebhookLogDto ToDto(this PaymentWebhookLog log) => new()
    {
        Id = log.Id,
        Source = log.Source,
        SignatureValid = log.SignatureValid,
        Outcome = log.Outcome,
        Reason = log.Reason,
        OrderCode = log.OrderCode,
        Amount = log.Amount,
        ProviderEventId = log.ProviderEventId,
        PaymentTransactionId = log.PaymentTransactionId,
        ReceivedAt = log.ReceivedAt
    };

    public static NotificationDto ToDto(this Notification notification) => new()
    {
        Id = notification.Id,
        Type = notification.Type.ToString(),
        Title = notification.Title,
        Body = notification.Body,
        Link = notification.Link,
        IsRead = notification.IsRead,
        CreatedAt = notification.CreatedAt
    };
}
