using Microsoft.EntityFrameworkCore;
using NursingHome.BusinessObjects.Entities;

namespace NursingHome.BusinessObjects;

/// <summary>
/// MODULE CHĂM SÓC &amp; GIA ĐÌNH — người phụ trách: Huy.
/// Sự cố, bệnh nền/dị ứng/chế độ ăn, lịch thăm, kế hoạch chăm sóc, ADL, bữa ăn, đánh giá, phiếu khám, phản hồi.
/// Thêm DbSet và mapping của bảng mới ở ĐÂY (bảng đã có trong Database/01_Schema.sql mục 10).
/// Entity đặt ở Entities/Care*.cs. Xem docs/tasks/.
/// </summary>
public partial class NursingHomeContext
{
    /// <summary>S3-H1: sự cố.</summary>
    public DbSet<Incident> Incidents => Set<Incident>();

    /// <summary>S3-H2: bệnh nền, dị ứng, chế độ ăn.</summary>
    public DbSet<MedicalCondition> MedicalConditions => Set<MedicalCondition>();
    public DbSet<Allergy> Allergies => Set<Allergy>();
    public DbSet<DietPlan> DietPlans => Set<DietPlan>();

    /// <summary>S3-H3: lịch thăm.</summary>
    public DbSet<VisitBooking> VisitBookings => Set<VisitBooking>();

    /// <summary>S3-H4: kế hoạch chăm sóc, nhắc việc, ADL, bữa ăn.</summary>
    public DbSet<CarePlan> CarePlans => Set<CarePlan>();
    public DbSet<CarePlanItem> CarePlanItems => Set<CarePlanItem>();
    public DbSet<GeneratedCareTask> GeneratedCareTasks => Set<GeneratedCareTask>();
    public DbSet<TaskReminder> TaskReminders => Set<TaskReminder>();
    public DbSet<DailyCareLog> DailyCareLogs => Set<DailyCareLog>();
    public DbSet<MealLog> MealLogs => Set<MealLog>();

    /// <summary>S3-H5: ngưỡng sinh hiệu riêng, đánh giá định kỳ, phiếu khám bác sĩ.</summary>
    public DbSet<VitalThreshold> VitalThresholds => Set<VitalThreshold>();
    public DbSet<Assessment> Assessments => Set<Assessment>();
    public DbSet<DoctorVisit> DoctorVisits => Set<DoctorVisit>();

    /// <summary>S3-H6: phản hồi gia đình.</summary>
    public DbSet<Feedback> Feedbacks => Set<Feedback>();

    /// <summary>S4-H2: nhật ký AI &amp; xu hướng sinh hiệu.</summary>
    public DbSet<AiInsight> AiInsights => Set<AiInsight>();

    partial void ConfigureCare(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Incident>(entity =>
        {
            entity.ToTable("Incidents");
            entity.Property(e => e.Type).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.Severity).HasConversion<string>().HasMaxLength(10);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.Location).HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.ImmediateAction).HasMaxLength(2000);
            entity.Property(e => e.Resolution).HasMaxLength(2000);
            entity.Property(e => e.RowVersion).IsRowVersion();
            entity.HasOne(e => e.Resident).WithMany().HasForeignKey(e => e.ResidentId);
            entity.HasOne(e => e.ReportedBy).WithMany().HasForeignKey(e => e.ReportedById);
            entity.HasOne(e => e.ClosedBy).WithMany().HasForeignKey(e => e.ClosedById);
        });

        modelBuilder.Entity<MedicalCondition>(entity =>
        {
            entity.ToTable("MedicalConditions");
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.HasOne(e => e.Resident).WithMany().HasForeignKey(e => e.ResidentId);
            entity.HasOne(e => e.RecordedBy).WithMany().HasForeignKey(e => e.RecordedById);
        });

        modelBuilder.Entity<Allergy>(entity =>
        {
            entity.ToTable("Allergies");
            entity.Property(e => e.Allergen).HasMaxLength(150);
            entity.Property(e => e.Type).HasConversion<string>().HasMaxLength(10);
            entity.Property(e => e.Reaction).HasMaxLength(500);
            entity.Property(e => e.Severity).HasConversion<string>().HasMaxLength(10);
            entity.HasOne(e => e.Resident).WithMany().HasForeignKey(e => e.ResidentId);
            entity.HasOne(e => e.RecordedBy).WithMany().HasForeignKey(e => e.RecordedById);
        });

        modelBuilder.Entity<DietPlan>(entity =>
        {
            entity.ToTable("DietPlans");
            entity.Property(e => e.DietType).HasConversion<string>().HasMaxLength(30);
            entity.Property(e => e.Texture).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.Restrictions).HasMaxLength(1000);
            entity.HasOne(e => e.Resident).WithMany().HasForeignKey(e => e.ResidentId);
            entity.HasOne(e => e.PrescribedBy).WithMany().HasForeignKey(e => e.PrescribedById);
        });

        modelBuilder.Entity<VisitBooking>(entity =>
        {
            entity.ToTable("VisitBookings");
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.GuestNames).HasMaxLength(500);
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Property(e => e.ReviewNote).HasMaxLength(500);
            entity.HasOne(e => e.Resident).WithMany().HasForeignKey(e => e.ResidentId);
            entity.HasOne(e => e.FamilyAccount).WithMany().HasForeignKey(e => e.FamilyAccountId);
            entity.HasOne(e => e.ReviewedBy).WithMany().HasForeignKey(e => e.ReviewedById);
        });

        modelBuilder.Entity<CarePlan>(entity =>
        {
            entity.ToTable("CarePlans");
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.Goals).HasMaxLength(1000);
            entity.HasOne(e => e.Resident).WithMany().HasForeignKey(e => e.ResidentId);
            entity.HasOne(e => e.CreatedBy).WithMany().HasForeignKey(e => e.CreatedById);
            entity.HasMany(e => e.Items).WithOne(i => i.CarePlan).HasForeignKey(i => i.CarePlanId);
        });

        modelBuilder.Entity<CarePlanItem>(entity =>
        {
            entity.ToTable("CarePlanItems");
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.Instructions).HasMaxLength(1000);
            entity.Property(e => e.DaysOfWeek).HasMaxLength(7).IsFixedLength();
            entity.HasOne(e => e.DefaultAssignee).WithMany().HasForeignKey(e => e.DefaultAssigneeId);
        });

        modelBuilder.Entity<GeneratedCareTask>(entity =>
        {
            entity.ToTable("GeneratedCareTasks");
            entity.HasKey(e => new { e.CarePlanItemId, e.ForDate });
        });

        modelBuilder.Entity<TaskReminder>(entity =>
        {
            entity.ToTable("TaskReminders");
            entity.HasKey(e => new { e.CareTaskId, e.Kind });
            entity.Property(e => e.Kind).HasConversion<string>().HasMaxLength(10);
        });

        modelBuilder.Entity<DailyCareLog>(entity =>
        {
            entity.ToTable("DailyCareLogs");
            entity.Property(e => e.Category).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.Level).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.HasOne(e => e.Resident).WithMany().HasForeignKey(e => e.ResidentId);
            entity.HasOne(e => e.RecordedBy).WithMany().HasForeignKey(e => e.RecordedById);
        });

        modelBuilder.Entity<MealLog>(entity =>
        {
            entity.ToTable("MealLogs");
            entity.Property(e => e.Meal).HasConversion<string>().HasMaxLength(10);
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.HasOne(e => e.Resident).WithMany().HasForeignKey(e => e.ResidentId);
            entity.HasOne(e => e.RecordedBy).WithMany().HasForeignKey(e => e.RecordedById);
        });

        modelBuilder.Entity<VitalThreshold>(entity =>
        {
            entity.ToTable("VitalThresholds");
            entity.HasKey(e => e.ResidentId);
            entity.Property(e => e.TemperatureHigh).HasPrecision(4, 1);
            entity.Property(e => e.SpO2Low).HasPrecision(5, 2);
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.HasOne(e => e.Resident).WithMany().HasForeignKey(e => e.ResidentId);
            entity.HasOne(e => e.SetBy).WithMany().HasForeignKey(e => e.SetById);
        });

        modelBuilder.Entity<Assessment>(entity =>
        {
            entity.ToTable("Assessments");
            entity.Property(e => e.Type).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.RiskLevel).HasConversion<string>().HasMaxLength(10);
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.HasOne(e => e.Resident).WithMany().HasForeignKey(e => e.ResidentId);
            entity.HasOne(e => e.AssessedBy).WithMany().HasForeignKey(e => e.AssessedById);
        });

        modelBuilder.Entity<DoctorVisit>(entity =>
        {
            entity.ToTable("DoctorVisits");
            entity.Property(e => e.Subjective).HasMaxLength(2000);
            entity.Property(e => e.Objective).HasMaxLength(2000);
            entity.Property(e => e.Assessment).HasMaxLength(2000);
            entity.Property(e => e.Plan).HasColumnName("Plan").HasMaxLength(2000);
            entity.HasOne(e => e.Resident).WithMany().HasForeignKey(e => e.ResidentId);
            entity.HasOne(e => e.Doctor).WithMany().HasForeignKey(e => e.DoctorId);
        });

        modelBuilder.Entity<Feedback>(entity =>
        {
            entity.ToTable("Feedbacks");
            entity.Property(e => e.Category).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.Subject).HasMaxLength(200);
            entity.Property(e => e.Content).HasMaxLength(2000);
            entity.Property(e => e.Response).HasMaxLength(2000);
            entity.HasOne(e => e.FamilyAccount).WithMany().HasForeignKey(e => e.FamilyAccountId);
            entity.HasOne(e => e.Resident).WithMany().HasForeignKey(e => e.ResidentId);
            entity.HasOne(e => e.AssignedTo).WithMany().HasForeignKey(e => e.AssignedToId);
            entity.HasOne(e => e.RespondedBy).WithMany().HasForeignKey(e => e.RespondedById);
        });

        modelBuilder.Entity<AiInsight>(entity =>
        {
            entity.ToTable("AiInsights");
            entity.Property(e => e.Type).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(10);
            entity.HasOne(e => e.Resident).WithMany().HasForeignKey(e => e.ResidentId);
            entity.HasOne(e => e.ReviewedBy).WithMany().HasForeignKey(e => e.ReviewedById);
        });
    }
}
