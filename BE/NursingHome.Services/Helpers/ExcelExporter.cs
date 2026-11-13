using ClosedXML.Excel;

namespace NursingHome.Services.Helpers;

/// <summary>Một cột Excel: tiêu đề tiếng Việt, cách lấy giá trị, định dạng số (vd. "#,##0", "dd/MM/yyyy", "0.0").</summary>
public sealed record ExcelColumn<T>(string Header, Func<T, object?> Value, string? Format = null);

/// <summary>
/// Xuất bảng ra .xlsx bằng ClosedXML (MIT). Mỗi sheet: dòng 1 tiêu đề, dòng 2 ghi chú, dòng 4 tiêu đề cột, dữ liệu từ dòng 5.
/// Dùng: <c>new ExcelExporter().AddSheet("Doanh thu", "Doanh thu 2026", rows, columns).ToBytes()</c>.
/// </summary>
public sealed class ExcelExporter : IDisposable
{
    public const string MoneyFormat = "#,##0";
    public const string DateFormat = "dd/MM/yyyy";
    public const string PercentFormat = "0.0";

    private const int HeaderRow = 4;
    private readonly XLWorkbook workbook = new();

    public ExcelExporter AddSheet<T>(string sheetName, string title, IEnumerable<T> rows, IReadOnlyList<ExcelColumn<T>> columns, string? subtitle = null)
    {
        var sheet = workbook.Worksheets.Add(sheetName.Length > 31 ? sheetName[..31] : sheetName);
        sheet.Cell(1, 1).Value = title;
        sheet.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(14);
        sheet.Cell(2, 1).Value = subtitle ?? $"Xuất lúc {DateTime.UtcNow.AddHours(7):dd/MM/yyyy HH:mm} (giờ VN)";
        sheet.Cell(2, 1).Style.Font.SetItalic().Font.SetFontColor(XLColor.Gray);

        for (var c = 0; c < columns.Count; c++)
        {
            var cell = sheet.Cell(HeaderRow, c + 1);
            cell.Value = columns[c].Header;
            cell.Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#DCEFEA"))
                .Border.SetBottomBorder(XLBorderStyleValues.Thin);
        }

        var r = HeaderRow + 1;
        foreach (var row in rows)
        {
            for (var c = 0; c < columns.Count; c++)
            {
                var cell = sheet.Cell(r, c + 1);
                cell.Value = ToCellValue(columns[c].Value(row));
                if (columns[c].Format is { } format)
                {
                    cell.Style.NumberFormat.Format = format;
                }
            }

            r++;
        }

        sheet.SheetView.FreezeRows(HeaderRow);
        sheet.Columns(1, Math.Max(1, columns.Count)).AdjustToContents(HeaderRow, Math.Max(HeaderRow, r - 1));
        return this;
    }

    public byte[] ToBytes()
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public void Dispose() => workbook.Dispose();

    private static XLCellValue ToCellValue(object? value) => value switch
    {
        null => Blank.Value,
        string text => text,
        decimal number => number,
        double number => number,
        int number => number,
        long number => number,
        bool flag => flag,
        DateOnly date => date.ToDateTime(TimeOnly.MinValue),
        DateTime time => time,
        _ => value.ToString() ?? ""
    };
}
