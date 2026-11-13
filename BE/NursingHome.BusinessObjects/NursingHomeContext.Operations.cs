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

    // S3-V2 ca trực & phân công phụ trách
    public DbSet<Shift> Shifts => Set<Shift>();

    public DbSet<ShiftAssignment> ShiftAssignments => Set<ShiftAssignment>();

    public DbSet<ResidentCareAssignment> ResidentCareAssignments => Set<ResidentCareAssignment>();

    partial void ConfigureOperations(ModelBuilder modelBuilder)
    {
        ConfigureStaffing(modelBuilder);

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

    private static void ConfigureStaffing(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Shift>().ToTable("Shifts");
        modelBuilder.Entity<ShiftAssignment>(entity =>
        {
            entity.ToTable("ShiftAssignments");
            entity.HasOne(e => e.Account).WithMany().HasForeignKey(e => e.AccountId);
            entity.HasOne(e => e.Shift).WithMany().HasForeignKey(e => e.ShiftId);
        });
        modelBuilder.Entity<ResidentCareAssignment>(entity =>
        {
            entity.ToTable("ResidentCareAssignments");
            entity.HasOne(e => e.Resident).WithMany().HasForeignKey(e => e.ResidentId);
            entity.HasOne(e => e.Account).WithMany().HasForeignKey(e => e.AccountId);
            entity.HasOne(e => e.AssignedBy).WithMany().HasForeignKey(e => e.AssignedById);
            entity.HasOne(e => e.Shift).WithMany().HasForeignKey(e => e.ShiftId);
        });
    }
}
