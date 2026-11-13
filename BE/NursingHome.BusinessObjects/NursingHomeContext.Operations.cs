using Microsoft.EntityFrameworkCore;
using NursingHome.BusinessObjects.Entities;

namespace NursingHome.BusinessObjects;

/// <summary>
/// MODULE VẬN HÀNH &amp; TÀI CHÍNH — người phụ trách: Viên.
/// Tiếp nhận/tham quan, ca trực, bàn giao ca, báo cáo, tài chính mở rộng, chat, AI hạ tầng.
/// Thêm DbSet và mapping của bảng mới ở ĐÂY (bảng đã có trong Database/01_Schema.sql mục 9).
/// Entity đặt ở Entities/Operations*.cs. Xem docs/tasks/.
/// </summary>
public partial class NursingHomeContext
{
    // S3-V1 tiếp nhận & tham quan
    public DbSet<Inquiry> Inquiries => Set<Inquiry>();

    public DbSet<TourBooking> TourBookings => Set<TourBooking>();

    partial void ConfigureOperations(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Inquiry>(entity =>
        {
            entity.ToTable("Inquiries");
            entity.Property(e => e.Source).HasConversion<string>();
            entity.Property(e => e.Status).HasConversion<string>();
            entity.HasOne(e => e.InterestedPackage).WithMany().HasForeignKey(e => e.InterestedPackageId);
            entity.HasOne(e => e.AssignedTo).WithMany().HasForeignKey(e => e.AssignedToId);
            entity.HasOne(e => e.Resident).WithMany().HasForeignKey(e => e.ResidentId);
            entity.HasMany(e => e.Tours).WithOne(e => e.Inquiry).HasForeignKey(e => e.InquiryId);
        });
        modelBuilder.Entity<TourBooking>(entity =>
        {
            entity.ToTable("TourBookings");
            entity.Property(e => e.TimeSlot).HasConversion<string>();
            entity.Property(e => e.Status).HasConversion<string>();
            entity.HasOne(e => e.ConfirmedBy).WithMany().HasForeignKey(e => e.ConfirmedById);
        });
    }
}
