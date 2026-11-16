using ClosedXML.Excel;
using NursingHome.Services.Helpers;
using NursingHome.Services.Implementations;

namespace NursingHome.Tests;

/// <summary>S3-V3: làm tròn tỉ lệ % và file Excel (tiêu đề tiếng Việt, định dạng tiền).</summary>
public class ReportRuleTests
{
    [Theory]
    [InlineData(1, 3, 33.3)]
    [InlineData(2, 3, 66.7)]
    [InlineData(52, 104, 50.0)]
    [InlineData(5, 4, 100.0)] // đổi giường trong ngày có thể đếm dư: chặn ở 100%
    public void Percent_RoundsToOneDecimal(int part, int whole, double expected) =>
        Assert.Equal((decimal)expected, ReportService.Percent(part, whole));

    [Fact]
    public void Percent_NullWhenNothingToCompare() => Assert.Null(ReportService.Percent(0, 0));

    [Fact]
    public void Excel_HasVietnameseHeadersAndMoneyFormat()
    {
        var rows = new[] { (Name: "Nguyễn Văn An", Amount: 6_300_000m, Due: new DateOnly(2026, 11, 10)) };
        using var exporter = new ExcelExporter().AddSheet("Công nợ", "Hóa đơn còn nợ", rows,
        [
            new("Cư dân", r => r.Name),
            new("Còn nợ (đ)", r => r.Amount, ExcelExporter.MoneyFormat),
            new("Hạn", r => r.Due, ExcelExporter.DateFormat)
        ]);

        using var workbook = new XLWorkbook(new MemoryStream(exporter.ToBytes()));
        var sheet = workbook.Worksheet("Công nợ");
        Assert.Equal("Hóa đơn còn nợ", sheet.Cell(1, 1).GetString());
        Assert.Equal("Còn nợ (đ)", sheet.Cell(4, 2).GetString());
        Assert.Equal(6_300_000m, sheet.Cell(5, 2).GetValue<decimal>());
        Assert.Equal("#,##0", sheet.Cell(5, 2).Style.NumberFormat.Format);
        Assert.Equal(new DateTime(2026, 11, 10), sheet.Cell(5, 3).GetDateTime());
    }
}
