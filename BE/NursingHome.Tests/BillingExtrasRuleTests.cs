using NursingHome.BusinessObjects.Entities;
using NursingHome.DTOs.Billing;
using NursingHome.Services.Helpers;
using NursingHome.Services.Implementations;
using NursingHome.Services.Options;

namespace NursingHome.Tests;

/// <summary>S3-V5: mốc nhắc nợ, lý do bỏ qua khi tạo hóa đơn hàng loạt, kiểm lần sử dụng, PDF.</summary>
public class BillingExtrasRuleTests
{
    private static readonly DateOnly Today = new(2026, 11, 16);

    [Theory]
    [InlineData(4, null)]
    [InlineData(3, ReminderStage.Before3Days)]
    [InlineData(0, ReminderStage.Before3Days)]
    [InlineData(-1, ReminderStage.Overdue)]
    [InlineData(-6, ReminderStage.Overdue)]
    [InlineData(-7, ReminderStage.Overdue7)]
    [InlineData(-40, ReminderStage.Overdue7)]
    public void StageFor_FollowsDaysToDueDate(int daysLeft, ReminderStage? expected) =>
        Assert.Equal(expected, InvoiceAutomationService.StageFor(Today.AddDays(daysLeft), Today));

    [Fact]
    public void SkipReason_ExistingInvoiceDischargedOrAdmittedLater()
    {
        var period = new DateOnly(2026, 12, 1);
        var periodEnd = new DateOnly(2026, 12, 31);
        var active = new Resident { Id = 1, AdmittedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) };
        var discharged = new Resident { Id = 2, AdmittedAt = active.AdmittedAt, DischargedAt = new DateTime(2026, 11, 20, 3, 0, 0, DateTimeKind.Utc) };
        // Ngừng lưu trú 01/12 lúc 08:00 giờ VN → vẫn có 1 ngày trong kỳ, không bỏ qua.
        var dischargedInPeriod = new Resident { Id = 3, AdmittedAt = active.AdmittedAt, DischargedAt = new DateTime(2026, 12, 1, 1, 0, 0, DateTimeKind.Utc) };
        var later = new Resident { Id = 4, AdmittedAt = new DateTime(2027, 1, 2, 0, 0, 0, DateTimeKind.Utc) };
        var existing = new Dictionary<int, string> { [5] = "INV-202612-00005" };

        Assert.Null(InvoiceAutomationService.SkipReason(active, period, periodEnd, existing));
        Assert.Contains("ngừng lưu trú", InvoiceAutomationService.SkipReason(discharged, period, periodEnd, existing));
        Assert.Null(InvoiceAutomationService.SkipReason(dischargedInPeriod, period, periodEnd, existing));
        Assert.NotNull(InvoiceAutomationService.SkipReason(later, period, periodEnd, existing));
        Assert.Contains("INV-202612-00005", InvoiceAutomationService.SkipReason(new Resident { Id = 5, AdmittedAt = active.AdmittedAt }, period, periodEnd, existing));
    }

    [Fact]
    public void ValidateUsage_QuantityAndDates()
    {
        Assert.Null(ExtraServiceService.ValidateUsage(2.5m, Today, Today));
        Assert.NotNull(ExtraServiceService.ValidateUsage(2.345m, Today, Today));
        Assert.NotNull(ExtraServiceService.ValidateUsage(0, Today, Today));
        Assert.NotNull(ExtraServiceService.ValidateUsage(1, Today.AddDays(1), Today));
        Assert.NotNull(ExtraServiceService.ValidateUsage(1, Today.AddDays(-ExtraServiceService.MaxBackdateDays - 1), Today));
    }

    /// <summary>
    /// Hồi quy: [Range(typeof(decimal), "0.01", ...)] parse giới hạn theo culture hiện tại; ở vi-VN dấu thập phân là ","
    /// nên ném ArgumentException (500). Hai DTO phải dùng ParseLimitsInInvariantCulture.
    /// </summary>
    [Fact]
    public void DecimalRanges_ValidateUnderVietnameseCulture()
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("vi-VN");
        try
        {
            Assert.True(IsValid(new CreateServiceUsageRequest { ExtraServiceId = 1, Quantity = 2.5m }));
            Assert.False(IsValid(new CreateServiceUsageRequest { ExtraServiceId = 1, Quantity = 0 }));
            Assert.True(IsValid(new AddInvoiceItemRequest { Description = "Phụ thu", Quantity = 1.5m, UnitPrice = 100000 }));
            Assert.False(IsValid(new AddInvoiceItemRequest { Description = "Phụ thu", Quantity = 0.001m, UnitPrice = 1 }));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }

    private static bool IsValid(object model) =>
        System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
            model, new System.ComponentModel.DataAnnotations.ValidationContext(model), [], validateAllProperties: true);

    [Fact]
    public void InvoicePdf_RendersAPdfWithVietnameseText()
    {
        var invoice = new InvoiceDetailDto
        {
            Id = 1,
            Number = "INV-202611-00001",
            ResidentCode = "NH-0001",
            ResidentName = "Nguyễn Văn An",
            BillingPeriod = new DateOnly(2026, 11, 1),
            DueDate = new DateOnly(2026, 11, 10),
            Status = "PartiallyPaid",
            Total = 12105000,
            Paid = 6000000,
            Remaining = 6105000,
            Items =
            [
                new InvoiceItemDto { Description = "Gói Tiêu chuẩn: 01/11–30/11 (30 ngày)", Quantity = 1, UnitPrice = 12000000, Amount = 12000000 },
                new InvoiceItemDto { Description = "Giặt ủi 10/11/2026 (kg)", Quantity = 3.5m, UnitPrice = 30000, Amount = 105000 }
            ],
            Payments = [new PaymentDto { Amount = 6000000, Method = "Cash", PaidAt = new DateTime(2026, 11, 5, 2, 0, 0, DateTimeKind.Utc), Reference = "PT-01" }]
        };

        var bytes = InvoicePdf.Render(invoice, new FacilityOptions { Name = "Viện Dưỡng Lão An Khang", Hotline = "0236 000 0000" }, "http://localhost:5173/family/invoices/1");

        Assert.True(bytes.Length > 1000);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.Equal("12.105.000 đ", InvoicePdf.Money(12105000));
    }
}
