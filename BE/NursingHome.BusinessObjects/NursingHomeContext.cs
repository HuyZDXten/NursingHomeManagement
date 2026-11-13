using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Configuration;
using NursingHome.BusinessObjects.Entities;

namespace NursingHome.BusinessObjects;

/// <summary>
/// Database-first: schema nằm ở Database/01_Schema.sql, context này CHỈ mapping, không có migration.
/// Thêm cột/bảng: sửa 01_Schema.sql + Entity + phần mapping bên dưới trong cùng một commit.
/// Các DAO singleton tự new context ngắn hạn cho mỗi lệnh (giống ASS2), connection string lấy
/// từ appsettings.json / appsettings.{env}.json / biến môi trường.
/// </summary>
/// <remarks>
/// partial: bảng mới của Sprint 3+ KHÔNG thêm vào file này mà vào file của module mình
/// (NursingHomeContext.Operations.cs — Viên, NursingHomeContext.Care.cs — Huy) để tránh conflict.
/// </remarks>
public partial class NursingHomeContext : DbContext
{
    public NursingHomeContext()
    {
    }

    public NursingHomeContext(DbContextOptions<NursingHomeContext> options) : base(options)
    {
    }

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Resident> Residents => Set<Resident>();
    public DbSet<FamilyContact> FamilyContacts => Set<FamilyContact>();
    public DbSet<ResidentFamilyLink> ResidentFamilyLinks => Set<ResidentFamilyLink>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Bed> Beds => Set<Bed>();
    public DbSet<BedAssignment> BedAssignments => Set<BedAssignment>();
    public DbSet<CareTask> CareTasks => Set<CareTask>();
    public DbSet<CareRecord> CareRecords => Set<CareRecord>();
    public DbSet<VitalSign> VitalSigns => Set<VitalSign>();
    public DbSet<MedicationSchedule> MedicationSchedules => Set<MedicationSchedule>();
    public DbSet<MedicationLog> MedicationLogs => Set<MedicationLog>();
    public DbSet<ServicePackage> ServicePackages => Set<ServicePackage>();
    public DbSet<ResidentSubscription> ResidentSubscriptions => Set<ResidentSubscription>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<PaymentWebhookLog> PaymentWebhookLogs => Set<PaymentWebhookLog>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (optionsBuilder.IsConfigured)
        {
            return;
        }

        optionsBuilder.UseSqlServer(GetConnectionString());
    }

    /// <summary>
    /// Web SDK chép appsettings*.json cạnh API đã build, nên ưu tiên thư mục output, sau đó thư mục
    /// hiện tại. Biến môi trường ConnectionStrings__DefaultConnection ghi đè file.
    /// </summary>
    public static string GetConnectionString()
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        var basePath = File.Exists(Path.Combine(AppContext.BaseDirectory, "appsettings.json"))
            ? AppContext.BaseDirectory
            : Directory.GetCurrentDirectory();

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        return configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Thiếu ConnectionStrings:DefaultConnection trong appsettings.json.");
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // DATETIME2 lưu UTC; đánh dấu Kind = Utc khi đọc để JSON trả về có hậu tố "Z".
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>().ToTable("Accounts");
        modelBuilder.Entity<RefreshToken>().ToTable("RefreshTokens");
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("Notifications");
            entity.Property(e => e.Type).HasConversion<string>();
        });

        modelBuilder.Entity<Resident>(entity =>
        {
            entity.ToTable("Residents");
            entity.Property(e => e.Status).HasConversion<string>();
            entity.HasMany(e => e.BedAssignments).WithOne(e => e.Resident).HasForeignKey(e => e.ResidentId);
        });
        modelBuilder.Entity<FamilyContact>().ToTable("FamilyContacts");
        modelBuilder.Entity<ResidentFamilyLink>(entity =>
        {
            entity.ToTable("ResidentFamilyLinks");
            entity.HasOne(e => e.Resident).WithMany().HasForeignKey(e => e.ResidentId);
            entity.HasOne(e => e.FamilyAccount).WithMany().HasForeignKey(e => e.FamilyAccountId);
        });

        modelBuilder.Entity<Room>(entity =>
        {
            entity.ToTable("Rooms");
            entity.HasMany(e => e.Beds).WithOne(e => e.Room).HasForeignKey(e => e.RoomId);
        });
        modelBuilder.Entity<Bed>(entity =>
        {
            entity.ToTable("Beds");
            entity.Property(e => e.Status).HasConversion<string>();
            entity.Property(e => e.RowVersion).IsRowVersion();
            entity.HasMany(e => e.Assignments).WithOne(e => e.Bed).HasForeignKey(e => e.BedId);
        });
        modelBuilder.Entity<BedAssignment>(entity =>
        {
            entity.ToTable("BedAssignments");
            entity.HasOne(e => e.AssignedBy).WithMany().HasForeignKey(e => e.AssignedById);
        });

        modelBuilder.Entity<CareTask>(entity =>
        {
            entity.ToTable("CareTasks");
            entity.Property(e => e.Status).HasConversion<string>();
            entity.Property(e => e.RowVersion).IsRowVersion();
            entity.HasOne(e => e.Resident).WithMany().HasForeignKey(e => e.ResidentId);
            entity.HasOne(e => e.AssignedTo).WithMany().HasForeignKey(e => e.AssignedToId);
        });
        modelBuilder.Entity<CareRecord>(entity =>
        {
            entity.ToTable("CareRecords");
            entity.Property(e => e.Status).HasConversion<string>();
            entity.HasOne(e => e.CareTask).WithMany().HasForeignKey(e => e.CareTaskId);
            entity.HasOne(e => e.RecordedBy).WithMany().HasForeignKey(e => e.RecordedById);
        });
        modelBuilder.Entity<VitalSign>(entity =>
        {
            entity.ToTable("VitalSigns");
            entity.Property(e => e.Temperature).HasPrecision(4, 1);
            entity.Property(e => e.SpO2).HasPrecision(5, 2);
            entity.HasOne(e => e.RecordedBy).WithMany().HasForeignKey(e => e.RecordedById);
        });

        modelBuilder.Entity<MedicationSchedule>(entity =>
        {
            entity.ToTable("MedicationSchedules");
            entity.Property(e => e.ScheduledTime).HasColumnType("time(0)");
            entity.HasOne(e => e.Resident).WithMany().HasForeignKey(e => e.ResidentId);
            entity.HasOne(e => e.EnteredBy).WithMany().HasForeignKey(e => e.EnteredById);
        });
        modelBuilder.Entity<MedicationLog>(entity =>
        {
            entity.ToTable("MedicationLogs");
            entity.Property(e => e.Status).HasConversion<string>();
            entity.Property(e => e.RowVersion).IsRowVersion();
            entity.HasOne(e => e.MedicationSchedule).WithMany().HasForeignKey(e => e.MedicationScheduleId);
            entity.HasOne(e => e.RecordedBy).WithMany().HasForeignKey(e => e.RecordedById);
        });

        modelBuilder.Entity<ServicePackage>(entity =>
        {
            entity.ToTable("ServicePackages");
            entity.Property(e => e.MonthlyPrice).HasPrecision(18, 0);
        });
        modelBuilder.Entity<ResidentSubscription>(entity =>
        {
            entity.ToTable("ResidentSubscriptions");
            entity.Property(e => e.AgreedMonthlyPrice).HasPrecision(18, 0);
            entity.HasOne(e => e.Resident).WithMany().HasForeignKey(e => e.ResidentId);
            entity.HasOne(e => e.ServicePackage).WithMany().HasForeignKey(e => e.ServicePackageId);
        });
        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.ToTable("Invoices");
            entity.Property(e => e.Status).HasConversion<string>();
            entity.Property(e => e.RowVersion).IsRowVersion();
            entity.HasOne(e => e.Resident).WithMany().HasForeignKey(e => e.ResidentId);
            entity.HasMany(e => e.Items).WithOne().HasForeignKey(e => e.InvoiceId);
            entity.HasMany(e => e.Payments).WithOne().HasForeignKey(e => e.InvoiceId);
        });
        modelBuilder.Entity<InvoiceItem>(entity =>
        {
            entity.ToTable("InvoiceItems");
            entity.Property(e => e.Quantity).HasPrecision(9, 2);
            entity.Property(e => e.UnitPrice).HasPrecision(18, 0);
        });
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.ToTable("Payments");
            entity.Property(e => e.Amount).HasPrecision(18, 0);
            entity.Property(e => e.Method).HasConversion<string>();
        });
        modelBuilder.Entity<PaymentTransaction>(entity =>
        {
            entity.ToTable("PaymentTransactions");
            entity.Property(e => e.Amount).HasPrecision(18, 0);
            entity.Property(e => e.Status).HasConversion<string>();
            entity.Property(e => e.RowVersion).IsRowVersion();
            entity.HasOne(e => e.Invoice).WithMany().HasForeignKey(e => e.InvoiceId);
        });
        modelBuilder.Entity<PaymentWebhookLog>(entity =>
        {
            entity.ToTable("PaymentWebhookLogs");
            entity.Property(e => e.Amount).HasPrecision(18, 0);
        });

        // Mapping của các module Sprint 3+ (mỗi người một file partial).
        ConfigureOperations(modelBuilder);
        ConfigureCare(modelBuilder);

        // Không xóa dây chuyền: lịch sử được giữ, FK trong DB là NO ACTION. (Chạy SAU mọi mapping.)
        foreach (var foreignKey in modelBuilder.Model.GetEntityTypes().SelectMany(type => type.GetForeignKeys()))
        {
            foreignKey.DeleteBehavior = DeleteBehavior.NoAction;
        }
    }

    /// <summary>Module Vận hành &amp; Tài chính (Viên) — cài đặt ở NursingHomeContext.Operations.cs.</summary>
    partial void ConfigureOperations(ModelBuilder modelBuilder);

    /// <summary>Module Chăm sóc &amp; Gia đình (Huy) — cài đặt ở NursingHomeContext.Care.cs.</summary>
    partial void ConfigureCare(ModelBuilder modelBuilder);

    public sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        value => value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : value,
        value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
