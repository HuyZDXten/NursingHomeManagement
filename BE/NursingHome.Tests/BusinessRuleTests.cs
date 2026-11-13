using System.Text.Json;
using Microsoft.Extensions.Options;
using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Helpers;
using NursingHome.DTOs.Care;
using NursingHome.Services.Helpers;
using NursingHome.Services.Implementations;
using NursingHome.Services.Options;
using NursingHome.Services.Payments;

namespace NursingHome.Tests;

/// <summary>Test các quy tắc thuần (không cần DB). Chạy: dotnet test BE/NursingHome.sln</summary>
public class InvoiceMathTests
{
    [Fact]
    public void FullMonth_ChargesWholePrice()
    {
        var line = InvoiceMath.ProrateMonth(new DateOnly(2026, 10, 1), new DateOnly(2026, 1, 1), null,
            new DateOnly(2026, 1, 1), null, 12_000_000m);
        Assert.NotNull(line);
        Assert.Equal(31, line.Value.Days);
        Assert.Equal(12_000_000m, line.Value.Amount);
    }

    [Fact]
    public void AdmittedMidMonth_CountsFirstAndLastDay()
    {
        // Nhập viện 21/11 → 21..30 = 10 ngày / 30 ngày.
        var line = InvoiceMath.ProrateMonth(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 21), null,
            new DateOnly(2026, 11, 21), null, 9_000_000m);
        Assert.Equal(10, line!.Value.Days);
        Assert.Equal(3_000_000m, line.Value.Amount);
    }

    [Fact]
    public void DischargedBeforePeriod_NoCharge()
    {
        var line = InvoiceMath.ProrateMonth(new DateOnly(2026, 11, 1), new DateOnly(2026, 1, 1), null,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 10, 15), 8_000_000m);
        Assert.Null(line);
    }

    [Fact]
    public void Remaining_RoundsEachLineToWholeVnd()
    {
        var invoice = new Invoice
        {
            Items = [new InvoiceItem { Quantity = 1.5m, UnitPrice = 333 }, new InvoiceItem { Quantity = 2, UnitPrice = 150_000 }],
            Payments = [new Payment { Amount = 100_000 }]
        };
        Assert.Equal(500m + 300_000m - 100_000m, InvoiceMath.Remaining(invoice)); // 499.5 → 500
    }
}

public class PayOsSignatureTests
{
    private const string Key = "nursinghome-demo-checksum-key";

    private static PayOsClient Client() => new(new HttpClient(), Options.Create(new PayOsOptions
    {
        Enabled = true,
        ClientId = "id",
        ApiKey = "key",
        ChecksumKey = Key
    }));

    private static string Webhook(object data, string? signature = null)
    {
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(data));
        signature ??= PayOsClient.Sign(Key, PayOsClient.SignedText(doc.RootElement));
        return JsonSerializer.Serialize(new { code = "00", desc = "success", success = true, data, signature });
    }

    [Fact]
    public void SignedText_SortsKeys_AndBlanksNulls()
    {
        using var doc = JsonDocument.Parse("""{"orderCode":2001,"amount":5000,"description":"NH2","counterAccountName":null,"desc":"null"}""");
        Assert.Equal("amount=5000&counterAccountName=&desc=&description=NH2&orderCode=2001", PayOsClient.SignedText(doc.RootElement));
    }

    [Fact]
    public void ValidWebhook_IsAccepted()
    {
        var hook = Client().VerifyWebhook(Webhook(new { orderCode = 2001, amount = 6_300_000, description = "NH2", reference = "FT1", code = "00" }));
        Assert.True(hook.SignatureValid);
        Assert.True(hook.Success);
        Assert.Equal(2001, hook.OrderCode);
        Assert.Equal(6_300_000m, hook.Amount);
    }

    [Fact]
    public void TamperedAmount_IsRejected()
    {
        string valid = Webhook(new { orderCode = 2001, amount = 6_300_000, reference = "FT1" });
        var hook = Client().VerifyWebhook(valid.Replace("6300000", "6300001"));
        Assert.False(hook.SignatureValid);
    }
}

public class SecurityHelperTests
{
    [Fact]
    public void PasswordHasher_VerifiesOnlyMatchingPassword()
    {
        string hash = PasswordHasher.Hash("Demo@123456");
        Assert.True(PasswordHasher.Verify("Demo@123456", hash));
        Assert.False(PasswordHasher.Verify("demo@123456", hash));
        Assert.False(PasswordHasher.Verify("Demo@123456", "Demo@123456")); // không chấp nhận plaintext trong DB
    }

    [Fact]
    public void Otp_ExpiresAfterFiveWrongAttempts()
    {
        var store = new PasswordResetOtpStore(TimeProvider.System);
        string code = store.Issue("a@b.c")!;
        Assert.Null(store.Issue("a@b.c")); // đang trong thời gian chờ gửi lại
        for (int i = 0; i < 5; i++)
        {
            Assert.False(store.Verify("a@b.c", "000000" == code ? "111111" : "000000"));
        }

        Assert.False(store.Verify("a@b.c", code));
    }

    [Fact]
    public void FacilityClock_ConvertsVietnamTimeToUtc()
    {
        var clock = new FacilityClock(TimeProvider.System);
        var utc = clock.ToUtc(new DateOnly(2026, 11, 13), new TimeOnly(8, 0));
        Assert.Equal(new DateTime(2026, 11, 13, 1, 0, 0, DateTimeKind.Utc), utc);
    }

    [Fact]
    public void VitalAlerts_FlagOnlyOutOfRangeValues()
    {
        var limits = new VitalAlertOptions();
        var normal = new RecordVitalSignRequest { Systolic = 120, Diastolic = 80, HeartRate = 75, Temperature = 36.6m, SpO2 = 98 };
        var fever = new RecordVitalSignRequest { Systolic = 120, Diastolic = 80, HeartRate = 75, Temperature = 38.5m, SpO2 = 90 };
        Assert.Empty(HealthService.OutOfRange(normal, limits));
        Assert.Equal(2, HealthService.OutOfRange(fever, limits).Count);
    }
}
