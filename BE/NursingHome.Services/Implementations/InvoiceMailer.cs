using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NursingHome.BusinessObjects.Entities;
using NursingHome.DTOs.Billing;
using NursingHome.Repositories.Interfaces;
using NursingHome.Services.Helpers;
using NursingHome.Services.Interfaces;
using NursingHome.Services.Options;

namespace NursingHome.Services.Implementations;

/// <summary>
/// S3-V5 (UC-118): email hóa đơn phát hành và nhắc nợ cho tài khoản gia đình có quyền Billing.
/// Chỉ gửi khi đã cấu hình SMTP; lỗi gửi chỉ ghi log, không làm hỏng thao tác chính.
/// </summary>
public class InvoiceMailer(
    IResidentRepository residents,
    IEmailService email,
    IOptions<EmailOptions> emailOptions,
    IOptions<FacilityOptions> facility,
    ILogger<InvoiceMailer> logger) : IInvoiceMailer
{
    public Task<int> SendIssuedAsync(InvoiceListItemDto invoice) =>
        SendAsync(invoice, $"Hóa đơn {invoice.Number} — {facility.Value.Name}",
            $"Hóa đơn tháng {invoice.BillingPeriod:MM/yyyy} của <b>{Encode(invoice.ResidentName)}</b> đã phát hành.");

    public Task<int> SendReminderAsync(InvoiceListItemDto invoice, ReminderStage stage) =>
        SendAsync(invoice, $"Nhắc thanh toán hóa đơn {invoice.Number} ({stage.ToVietnamese()})",
            stage == ReminderStage.Before3Days
                ? $"Hóa đơn của <b>{Encode(invoice.ResidentName)}</b> sắp đến hạn thanh toán."
                : $"Hóa đơn của <b>{Encode(invoice.ResidentName)}</b> {stage.ToVietnamese()}. Nếu gia đình đã thanh toán, vui lòng bỏ qua email này.");

    private async Task<int> SendAsync(InvoiceListItemDto invoice, string subject, string lead)
    {
        if (!email.IsConfigured)
        {
            return 0;
        }

        var link = $"{emailOptions.Value.FrontendBaseUrl.TrimEnd('/')}/family/invoices/{invoice.Id}";
        var body = $"""
            <p>Kính gửi gia đình,</p>
            <p>{lead}</p>
            <table cellpadding="4">
              <tr><td>Số hóa đơn</td><td><b>{Encode(invoice.Number)}</b></td></tr>
              <tr><td>Tổng tiền</td><td>{InvoicePdf.Money(invoice.Total)}</td></tr>
              <tr><td>Còn phải trả</td><td><b>{InvoicePdf.Money(invoice.Remaining)}</b></td></tr>
              <tr><td>Hạn thanh toán</td><td>{invoice.DueDate:dd/MM/yyyy}</td></tr>
            </table>
            <p>Xem chi tiết, tải PDF và thanh toán VietQR: <a href="{link}">{link}</a></p>
            <p>{Encode(facility.Value.Name)}{(string.IsNullOrWhiteSpace(facility.Value.Hotline) ? "" : $" · Hotline {Encode(facility.Value.Hotline)}")}</p>
            """;

        var sent = 0;
        foreach (var address in await residents.FamilyEmailsAsync(invoice.ResidentId, requireBilling: true))
        {
            try
            {
                await email.SendAsync(address, subject, body);
                sent++;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Không gửi được email hóa đơn {Number} tới gia đình", invoice.Number);
            }
        }

        return sent;
    }

    private static string Encode(string? text) => WebUtility.HtmlEncode(text ?? "");
}
