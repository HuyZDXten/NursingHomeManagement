using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Exceptions;
using NursingHome.DTOs.Residents;
using NursingHome.Services.Helpers;

namespace NursingHome.Tests;

/// <summary>BL-V2: giấy tờ, tạm vắng, tái nhập viện, hợp đồng.</summary>
public class ResidentRecordsTests
{
    private static readonly byte[] Pdf = "%PDF-1.4\n1 0 obj"u8.ToArray();
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");
    private static readonly DateTime Now = new(2026, 11, 16, 3, 0, 0, DateTimeKind.Utc); // 10:00 giờ cơ sở

    [Fact]
    public void Document_AcceptsPdfAndImagesBySignature()
    {
        Assert.Equal((".pdf", "application/pdf"), ResidentRecordRules.ValidateDocument("cccd.pdf", Pdf.Length, Pdf));
        Assert.Equal((".png", "image/png"), ResidentRecordRules.ValidateDocument("cccd.png", Png.Length, Png));
        // Đuôi .pdf nhưng nội dung là ảnh PNG → lưu và trả đúng kiểu ảnh.
        Assert.Equal((".png", "image/png"), ResidentRecordRules.ValidateDocument("scan.pdf", Png.Length, Png));
    }

    [Theory]
    [InlineData("hop-dong.docx", 100, "Chỉ nhận file PDF, JPG hoặc PNG.")]
    [InlineData("hop-dong.pdf", 0, "Chưa chọn file.")]
    [InlineData("hop-dong.pdf", ResidentRecordRules.MaxDocumentBytes + 1, "File tối đa 10 MB.")]
    public void Document_RejectsBadFiles(string name, long length, string message)
    {
        var error = Assert.Throws<BusinessRuleException>(() => ResidentRecordRules.ValidateDocument(name, length, Pdf));
        Assert.Equal(message, error.Message);
    }

    [Fact]
    public void Document_RejectsHtmlRenamedAsPdf()
    {
        var html = "<html><body>x</body></html>"u8.ToArray();
        Assert.Throws<BusinessRuleException>(() => ResidentRecordRules.ValidateDocument("x.pdf", html.Length, html));
    }

    [Fact]
    public void Leave_CannotStartInFutureOrReturnBeforeLeaving()
    {
        ResidentRecordRules.ValidateLeave(Now.AddHours(-2), new DateOnly(2026, 11, 20), Now);
        Assert.Throws<BusinessRuleException>(() => ResidentRecordRules.ValidateLeave(Now.AddHours(1), null, Now));
        Assert.Throws<BusinessRuleException>(() => ResidentRecordRules.ValidateLeave(Now, new DateOnly(2026, 11, 15), Now));
        // Ngày dự kiến về = chính ngày đi (giờ cơ sở) là hợp lệ.
        ResidentRecordRules.ValidateLeave(Now, new DateOnly(2026, 11, 16), Now);

        Assert.Throws<BusinessRuleException>(() => ResidentRecordRules.ValidateReturn(Now, Now.AddMinutes(-1), Now));
        Assert.Throws<BusinessRuleException>(() => ResidentRecordRules.ValidateReturn(Now.AddDays(-1), Now.AddHours(1), Now));
        ResidentRecordRules.ValidateReturn(Now.AddDays(-1), Now, Now);
    }

    [Fact]
    public void Readmit_StartBetweenDischargeAndThirtyDaysAhead()
    {
        var discharged = new DateOnly(2026, 10, 24);
        var today = new DateOnly(2026, 11, 16);
        ResidentRecordRules.ValidateReadmitStart(today, discharged, today);
        ResidentRecordRules.ValidateReadmitStart(discharged, discharged, today);
        Assert.Throws<BusinessRuleException>(() => ResidentRecordRules.ValidateReadmitStart(discharged.AddDays(-1), discharged, today));
        Assert.Throws<BusinessRuleException>(() => ResidentRecordRules.ValidateReadmitStart(today.AddDays(31), discharged, today));
    }

    [Fact]
    public void Contract_DatesAndWholeVnd()
    {
        var ok = new CreateContractRequest { Number = "HD-1", SignedOn = new DateOnly(2026, 11, 1), StartDate = new DateOnly(2026, 11, 1), MonthlyPrice = 12_000_000 };
        ResidentRecordRules.ValidateContract(ok);
        Assert.Throws<BusinessRuleException>(() => ResidentRecordRules.ValidateContract(new CreateContractRequest
        {
            Number = "HD-2", SignedOn = ok.SignedOn, StartDate = ok.StartDate, EndDate = ok.StartDate.AddDays(-1)
        }));
        Assert.Throws<BusinessRuleException>(() => ResidentRecordRules.ValidateContract(new CreateContractRequest
        {
            Number = "HD-3", SignedOn = ok.SignedOn, StartDate = ok.StartDate, MonthlyPrice = 12_000_000.5m
        }));
        Assert.Throws<BusinessRuleException>(() => ResidentRecordRules.EnsureWholeVnd(100.5m, "Số tiền cọc"));
    }

    [Fact]
    public void Leave_OverdueOnlyWhenOpenAndPastExpectedReturn()
    {
        var today = new DateOnly(2026, 11, 16);
        var open = new ResidentLeave { Kind = LeaveKind.Hospital, StartAt = Now.AddDays(-5), ExpectedReturnOn = today.AddDays(-1), Reason = "x" };
        Assert.True(open.ToDto(today).Overdue);
        Assert.False(new ResidentLeave { ExpectedReturnOn = today, StartAt = Now, Reason = "x" }.ToDto(today).Overdue);
        open.ReturnedAt = Now;
        var returned = open.ToDto(today);
        Assert.False(returned.Overdue);
        Assert.False(returned.IsOpen);
    }
}
