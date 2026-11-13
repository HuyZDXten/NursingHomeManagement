using Microsoft.EntityFrameworkCore;

namespace NursingHome.BusinessObjects;

/// <summary>
/// MODULE VẬN HÀNH &amp; TÀI CHÍNH — người phụ trách: Viên.
/// Tiếp nhận/tham quan, ca trực, bàn giao ca, báo cáo, tài chính mở rộng, chat, AI hạ tầng.
/// Thêm DbSet và mapping của bảng mới ở ĐÂY (bảng đã có trong Database/01_Schema.sql mục 9).
/// Entity đặt ở Entities/Operations*.cs. Xem docs/tasks/.
/// </summary>
public partial class NursingHomeContext
{
    // Ví dụ (S3-V1):
    // public DbSet<Inquiry> Inquiries => Set<Inquiry>();

    partial void ConfigureOperations(ModelBuilder modelBuilder)
    {
        // Ví dụ:
        // modelBuilder.Entity<Inquiry>(entity =>
        // {
        //     entity.ToTable("Inquiries");
        //     entity.Property(e => e.Status).HasConversion<string>();
        // });
    }
}
