using NursingHome.BusinessObjects.Entities;
using NursingHome.DTOs.Admissions;

namespace NursingHome.Services.Helpers;

/// <summary>S3-V1 tiếp nhận &amp; tham quan.</summary>
public static partial class OperationsMapper
{
    /// <summary>Cần Include InterestedPackage, AssignedTo, Resident, Tours (AdmissionDAO.WithDetails).</summary>
    public static InquiryDto ToDto(this Inquiry inquiry)
    {
        var lastTour = inquiry.Tours.OrderByDescending(tour => tour.VisitDate).ThenByDescending(tour => tour.Id).FirstOrDefault();
        return new InquiryDto
        {
            Id = inquiry.Id,
            FullName = inquiry.FullName,
            Phone = inquiry.Phone,
            Email = inquiry.Email,
            ElderName = inquiry.ElderName,
            ElderAge = inquiry.ElderAge,
            CareNeeds = inquiry.CareNeeds,
            InterestedPackageId = inquiry.InterestedPackageId,
            InterestedPackageName = inquiry.InterestedPackage?.Name,
            Source = inquiry.Source.ToString(),
            Status = inquiry.Status.ToString(),
            AssignedToId = inquiry.AssignedToId,
            AssignedToName = inquiry.AssignedTo?.FullName,
            Notes = inquiry.Notes,
            ResidentId = inquiry.ResidentId,
            ResidentCode = inquiry.Resident?.Code,
            TourCount = inquiry.Tours.Count,
            LastTourDate = lastTour?.VisitDate,
            LastTourSlot = lastTour?.TimeSlot.ToString(),
            LastTourStatus = lastTour?.Status.ToString(),
            CreatedAt = inquiry.CreatedAt,
            UpdatedAt = inquiry.UpdatedAt
        };
    }

    /// <summary>Cần Include Inquiry, ConfirmedBy.</summary>
    public static TourBookingDto ToDto(this TourBooking tour) => new()
    {
        Id = tour.Id,
        InquiryId = tour.InquiryId,
        FullName = tour.Inquiry.FullName,
        Phone = tour.Inquiry.Phone,
        Email = tour.Inquiry.Email,
        VisitDate = tour.VisitDate,
        TimeSlot = tour.TimeSlot.ToString(),
        GuestCount = tour.GuestCount,
        Note = tour.Note,
        Status = tour.Status.ToString(),
        ConfirmedById = tour.ConfirmedById,
        ConfirmedByName = tour.ConfirmedBy?.FullName,
        CreatedAt = tour.CreatedAt
    };

    public static string ToVietnamese(this TourSlot slot) => slot == TourSlot.Morning ? "buổi sáng (08:00–11:00)" : "buổi chiều (14:00–17:00)";
}
