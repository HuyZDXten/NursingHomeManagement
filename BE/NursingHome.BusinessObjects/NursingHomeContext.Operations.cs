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

    // S3-V4 nghỉ phép/đổi ca & bàn giao ca
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();

    public DbSet<ShiftHandover> ShiftHandovers => Set<ShiftHandover>();

    // S3-V5 dịch vụ phát sinh, nhắc nợ
    public DbSet<ExtraService> ExtraServices => Set<ExtraService>();

    public DbSet<ServiceUsage> ServiceUsages => Set<ServiceUsage>();

    public DbSet<InvoiceReminder> InvoiceReminders => Set<InvoiceReminder>();

    // S3-V6 chat gia đình – nhân viên
    public DbSet<Conversation> Conversations => Set<Conversation>();

    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    partial void ConfigureOperations(ModelBuilder modelBuilder)
    {
        ConfigureStaffing(modelBuilder);
        ConfigureBillingExtras(modelBuilder);
        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.ToTable("Conversations");
            entity.HasOne(e => e.Resident).WithMany().HasForeignKey(e => e.ResidentId);
            entity.HasOne(e => e.FamilyAccount).WithMany().HasForeignKey(e => e.FamilyAccountId);
            entity.HasMany(e => e.Messages).WithOne(e => e.Conversation).HasForeignKey(e => e.ConversationId);
        });
        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.ToTable("Messages");
            entity.HasOne(e => e.Sender).WithMany().HasForeignKey(e => e.SenderId);
        });

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

    private static void ConfigureBillingExtras(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ExtraService>(entity =>
        {
            entity.ToTable("ExtraServices");
            entity.Property(e => e.UnitPrice).HasPrecision(18, 0);
        });
        modelBuilder.Entity<ServiceUsage>(entity =>
        {
            entity.ToTable("ServiceUsages");
            entity.Property(e => e.Quantity).HasPrecision(9, 2);
            entity.Property(e => e.UnitPrice).HasPrecision(18, 0);
            entity.HasOne(e => e.Resident).WithMany().HasForeignKey(e => e.ResidentId);
            entity.HasOne(e => e.ExtraService).WithMany().HasForeignKey(e => e.ExtraServiceId);
            entity.HasOne(e => e.RecordedBy).WithMany().HasForeignKey(e => e.RecordedById);
            entity.HasOne(e => e.InvoiceItem).WithMany().HasForeignKey(e => e.InvoiceItemId);
        });
        modelBuilder.Entity<InvoiceReminder>(entity =>
        {
            entity.ToTable("InvoiceReminders");
            entity.Property(e => e.Stage).HasConversion<string>();
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
        modelBuilder.Entity<LeaveRequest>(entity =>
        {
            entity.ToTable("LeaveRequests");
            entity.Property(e => e.Type).HasConversion<string>();
            entity.Property(e => e.Status).HasConversion<string>();
            entity.HasOne(e => e.Account).WithMany().HasForeignKey(e => e.AccountId);
            entity.HasOne(e => e.SwapWithAccount).WithMany().HasForeignKey(e => e.SwapWithAccountId);
            entity.HasOne(e => e.ReviewedBy).WithMany().HasForeignKey(e => e.ReviewedById);
            entity.HasOne(e => e.ShiftAssignment).WithMany().HasForeignKey(e => e.ShiftAssignmentId);
            entity.HasOne(e => e.SwapShiftAssignment).WithMany().HasForeignKey(e => e.SwapShiftAssignmentId);
        });
        modelBuilder.Entity<ShiftHandover>(entity =>
        {
            entity.ToTable("ShiftHandovers");
            entity.HasOne(e => e.Shift).WithMany().HasForeignKey(e => e.ShiftId);
            entity.HasOne(e => e.FromAccount).WithMany().HasForeignKey(e => e.FromAccountId);
            entity.HasOne(e => e.AcknowledgedBy).WithMany().HasForeignKey(e => e.AcknowledgedById);
        });
    }
}
