using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NursingHomeManagement.Models;

namespace NursingHomeManagement.Data;

public sealed class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Resident> Residents => Set<Resident>();
    public DbSet<FamilyContact> FamilyContacts => Set<FamilyContact>();
    public DbSet<ResidentFamilyLink> ResidentFamilyLinks => Set<ResidentFamilyLink>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Bed> Beds => Set<Bed>();
    public DbSet<BedAssignment> BedAssignments => Set<BedAssignment>();
    public DbSet<VitalSign> VitalSigns => Set<VitalSign>();
    public DbSet<CareTask> CareTasks => Set<CareTask>();
    public DbSet<CareRecord> CareRecords => Set<CareRecord>();
    public DbSet<MedicationSchedule> MedicationSchedules => Set<MedicationSchedule>();
    public DbSet<MedicationLog> MedicationLogs => Set<MedicationLog>();
    public DbSet<ServicePackage> ServicePackages => Set<ServicePackage>();
    public DbSet<ResidentSubscription> ResidentSubscriptions => Set<ResidentSubscription>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        ConfigureIndexes(builder);
        ConfigureConstraints(builder);
        ConfigureConventions(builder);
    }

    private static void ConfigureIndexes(ModelBuilder builder)
    {
        builder.Entity<Resident>().HasIndex(entity => entity.Code).IsUnique();
        builder.Entity<Room>().HasIndex(entity => entity.Number).IsUnique();
        builder.Entity<Bed>().HasIndex(entity => new { entity.RoomId, entity.Number }).IsUnique();
        builder.Entity<ResidentFamilyLink>().HasIndex(entity => new { entity.ResidentId, entity.FamilyUserId }).IsUnique();
        builder.Entity<BedAssignment>().HasIndex(entity => entity.BedId).IsUnique().HasFilter("[EndedAt] IS NULL");
        builder.Entity<BedAssignment>().HasIndex(entity => entity.ResidentId).IsUnique().HasFilter("[EndedAt] IS NULL");
        builder.Entity<MedicationLog>().HasIndex(entity => new { entity.MedicationScheduleId, entity.ScheduledAt }).IsUnique();
        builder.Entity<Invoice>().HasIndex(entity => new { entity.ResidentId, entity.BillingPeriod }).IsUnique();
        builder.Entity<Invoice>().HasIndex(entity => entity.Number).IsUnique();
        builder.Entity<Payment>().HasIndex(entity => entity.Reference).IsUnique();
        builder.Entity<VitalSign>().HasIndex(entity => new { entity.ResidentId, entity.RecordedAt });
        builder.Entity<CareTask>().HasIndex(entity => new { entity.AssignedToId, entity.DueAt });
    }

    private static void ConfigureConstraints(ModelBuilder builder)
    {
        builder.Entity<Resident>().ToTable(t => t.HasCheckConstraint("CK_Resident_Dates", "[DischargedAt] IS NULL OR [DischargedAt] >= [AdmittedAt]"));
        builder.Entity<BedAssignment>().ToTable(t => t.HasCheckConstraint("CK_Assignment_Dates", "[EndedAt] IS NULL OR [EndedAt] >= [StartedAt]"));
        builder.Entity<VitalSign>().ToTable(t => t.HasCheckConstraint("CK_Vitals_Range", "[Systolic] BETWEEN 40 AND 300 AND [Diastolic] BETWEEN 20 AND 200 AND [Systolic] > [Diastolic] AND [HeartRate] BETWEEN 20 AND 250 AND [Temperature] BETWEEN 25 AND 45 AND ([SpO2] IS NULL OR [SpO2] BETWEEN 0 AND 100)"));
        builder.Entity<ServicePackage>().ToTable(t => t.HasCheckConstraint("CK_Package_Price", "[MonthlyPrice] >= 0"));
        builder.Entity<ResidentSubscription>().ToTable(t =>
        {
            t.HasCheckConstraint("CK_Subscription_Price", "[AgreedMonthlyPrice] >= 0");
            t.HasCheckConstraint("CK_Subscription_Dates", "[EndDate] IS NULL OR [EndDate] >= [StartDate]");
        });
        builder.Entity<MedicationSchedule>().ToTable(t => t.HasCheckConstraint("CK_Medication_Dates", "[EndDate] IS NULL OR [EndDate] >= [StartDate]"));
        builder.Entity<Invoice>().ToTable(t => t.HasCheckConstraint("CK_Invoice_Period", "DAY([BillingPeriod]) = 1"));
        builder.Entity<InvoiceItem>().ToTable(t => t.HasCheckConstraint("CK_Item_Amount", "[Quantity] > 0 AND [UnitPrice] >= 0"));
        builder.Entity<Payment>().ToTable(t => t.HasCheckConstraint("CK_Payment_Amount", "[Amount] > 0"));
        builder.Entity<Bed>().ToTable(table => table.HasCheckConstraint("CK_Bed_Status", "[Status] BETWEEN 0 AND 2"));
        builder.Entity<CareTask>().ToTable(table => table.HasCheckConstraint("CK_CareTask_Status", "[Status] BETWEEN 0 AND 3"));
        builder.Entity<MedicationLog>().ToTable(table => table.HasCheckConstraint("CK_MedicationLog_Status", "[Status] BETWEEN 0 AND 3"));
        builder.Entity<Invoice>().ToTable(table => table.HasCheckConstraint("CK_Invoice_Status", "[Status] BETWEEN 0 AND 4"));
    }

    private static void ConfigureConventions(ModelBuilder builder)
    {
        foreach (var entity in builder.Model.GetEntityTypes())
        {
            foreach (var foreignKey in entity.GetForeignKeys())
            {
                foreignKey.DeleteBehavior = DeleteBehavior.Restrict;
            }

            foreach (var property in entity.GetProperties())
            {
                if (property.ClrType == typeof(decimal) || property.ClrType == typeof(decimal?))
                {
                    property.SetPrecision(18);
                    property.SetScale(2);
                }
            }
        }
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>().Where(entry => entry.State == EntityState.Modified))
        {
            entry.Entity.UpdatedAt = DateTimeOffset.UtcNow;
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
