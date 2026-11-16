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
    }
}
