using System.Globalization;
using NursingHome.DTOs.Billing;
using NursingHome.Services.Options;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace NursingHome.Services.Helpers;

/// <summary>
/// S3-V5 (UC-88): hóa đơn PDF bằng QuestPDF (giấy phép Community — miễn phí cho đồ án/tổ chức doanh thu nhỏ).
/// Nội dung lấy từ <see cref="InvoiceDetailDto"/> nên trùng khớp màn hình chi tiết hóa đơn.
/// </summary>
public static class InvoicePdf
{
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");
    private const string Accent = "#2f7d6d";

    static InvoicePdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static string Money(decimal value) => $"{value.ToString("N0", Vietnamese)} đ";

    public static byte[] Render(InvoiceDetailDto invoice, FacilityOptions facility, string familyPortalUrl) =>
        Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.DefaultTextStyle(style => style.FontSize(10).FontFamily("Arial"));

            page.Header().Row(row =>
            {
                row.RelativeItem().Column(column =>
                {
                    column.Item().Text(facility.Name).FontSize(16).Bold().FontColor(Accent);
                    if (!string.IsNullOrWhiteSpace(facility.Address))
                    {
                        column.Item().Text(facility.Address);
                    }

                    if (!string.IsNullOrWhiteSpace(facility.Hotline))
                    {
                        column.Item().Text($"Hotline: {facility.Hotline}");
                    }
                });
                row.ConstantItem(200).AlignRight().Column(column =>
                {
                    column.Item().AlignRight().Text("HÓA ĐƠN DỊCH VỤ").FontSize(14).Bold();
                    column.Item().AlignRight().Text($"Số: {invoice.Number}");
                    column.Item().AlignRight().Text($"Trạng thái: {StatusLabel(invoice.Status)}");
                });
            });

            page.Content().PaddingVertical(16).Column(column =>
            {
                column.Spacing(10);
                column.Item().Row(row =>
                {
                    row.RelativeItem().Text(text =>
                    {
                        text.Span("Cư dân: ").SemiBold();
                        text.Span($"{invoice.ResidentCode} · {invoice.ResidentName}");
                    });
                    row.RelativeItem().AlignRight().Text(text =>
                    {
                        text.Span("Kỳ: ").SemiBold();
                        text.Span($"tháng {invoice.BillingPeriod:MM/yyyy}   ");
                        text.Span("Hạn thanh toán: ").SemiBold();
                        text.Span($"{invoice.DueDate:dd/MM/yyyy}");
                    });
                });

                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(26);
                        columns.RelativeColumn();
                        columns.ConstantColumn(50);
                        columns.ConstantColumn(90);
                        columns.ConstantColumn(100);
                    });
                    table.Header(header =>
                    {
                        HeaderCell(header.Cell()).Text("#");
                        HeaderCell(header.Cell()).Text("Nội dung");
                        HeaderCell(header.Cell()).AlignRight().Text("SL");
                        HeaderCell(header.Cell()).AlignRight().Text("Đơn giá");
                        HeaderCell(header.Cell()).AlignRight().Text("Thành tiền");
                    });
                    var index = 1;
                    foreach (var item in invoice.Items)
                    {
                        BodyCell(table.Cell()).Text($"{index++}");
                        BodyCell(table.Cell()).Text(item.Description);
                        BodyCell(table.Cell()).AlignRight().Text(item.Quantity.ToString("0.##", Vietnamese));
                        BodyCell(table.Cell()).AlignRight().Text(Money(item.UnitPrice));
                        BodyCell(table.Cell()).AlignRight().Text(Money(item.Amount));
                    }
                });

                column.Item().AlignRight().Width(240).Column(totals =>
                {
                    TotalRow(totals, "Tổng cộng", invoice.Total, bold: true);
                    TotalRow(totals, "Đã thanh toán", invoice.Paid, bold: false);
                    TotalRow(totals, "Còn phải trả", invoice.Remaining, bold: true);
                });

                if (invoice.Payments.Count > 0)
                {
                    column.Item().Text("Các lần thanh toán").SemiBold();
                    foreach (var payment in invoice.Payments)
                    {
                        column.Item().Text($"• {payment.PaidAt.AddHours(7):dd/MM/yyyy HH:mm} — {Money(payment.Amount)} ({MethodLabel(payment.Method)}, mã {payment.Reference})");
                    }
                }

                if (invoice.Remaining > 0)
                {
                    column.Item().Background(Colors.Grey.Lighten4).Padding(10).Column(note =>
                    {
                        note.Item().Text("Hướng dẫn thanh toán").SemiBold();
                        note.Item().Text($"Quét mã VietQR (PayOS) tại cổng gia đình: {familyPortalUrl}");
                        note.Item().Text("Hoặc thanh toán tiền mặt / chuyển khoản tại quầy lễ tân, ghi số hóa đơn khi chuyển khoản.");
                    });
                }
            });

            page.Footer().AlignCenter().Text(text =>
            {
                text.Span($"{facility.Name} · in lúc {DateTime.UtcNow.AddHours(7):dd/MM/yyyy HH:mm} · trang ").FontSize(8).FontColor(Colors.Grey.Darken1);
                text.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Darken1);
                text.Span("/").FontSize(8).FontColor(Colors.Grey.Darken1);
                text.TotalPages().FontSize(8).FontColor(Colors.Grey.Darken1);
            });
        })).GeneratePdf();

    private static IContainer HeaderCell(IContainer cell) =>
        cell.Background(Accent).PaddingVertical(5).PaddingHorizontal(4).DefaultTextStyle(style => style.FontColor(Colors.White).SemiBold());

    private static IContainer BodyCell(IContainer cell) =>
        cell.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(4).PaddingHorizontal(4);

    private static void TotalRow(ColumnDescriptor column, string label, decimal value, bool bold) =>
        column.Item().Row(row =>
        {
            var left = row.RelativeItem().Text(label);
            var right = row.ConstantItem(120).AlignRight().Text(Money(value));
            if (bold)
            {
                left.Bold();
                right.Bold();
            }
        });

    private static string StatusLabel(string status) => status switch
    {
        "Draft" => "Nháp",
        "Unpaid" => "Chưa thanh toán",
        "PartiallyPaid" => "Thanh toán một phần",
        "Paid" => "Đã thanh toán",
        "Cancelled" => "Đã hủy",
        _ => status
    };

    private static string MethodLabel(string method) => method switch
    {
        "Cash" => "tiền mặt",
        "BankTransfer" => "chuyển khoản",
        "PayOS" => "VietQR PayOS",
        _ => method
    };
}
