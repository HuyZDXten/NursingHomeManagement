using Microsoft.EntityFrameworkCore;

namespace NursingHome.BusinessObjects;

/// <summary>
/// MODULE CHĂM SÓC &amp; GIA ĐÌNH — người phụ trách: Huy.
/// Sự cố, bệnh nền/dị ứng/chế độ ăn, lịch thăm, kế hoạch chăm sóc, ADL, bữa ăn, đánh giá, phiếu khám, phản hồi.
/// Thêm DbSet và mapping của bảng mới ở ĐÂY (bảng đã có trong Database/01_Schema.sql mục 10).
/// Entity đặt ở Entities/Care*.cs. Xem docs/tasks/.
/// </summary>
public partial class NursingHomeContext
{
    // Ví dụ (S3-H1):
    // public DbSet<Incident> Incidents => Set<Incident>();

    partial void ConfigureCare(ModelBuilder modelBuilder)
    {
        // Ví dụ:
        // modelBuilder.Entity<Incident>(entity =>
        // {
        //     entity.ToTable("Incidents");
        //     entity.Property(e => e.Status).HasConversion<string>();
        //     entity.HasOne(e => e.Resident).WithMany().HasForeignKey(e => e.ResidentId);
        // });
    }
}
