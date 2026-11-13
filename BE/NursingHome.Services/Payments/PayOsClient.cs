using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using NursingHome.BusinessObjects.Exceptions;
using NursingHome.Services.Options;

namespace NursingHome.Services.Payments;

public sealed record PayOsLink(
    string? PaymentLinkId,
    string CheckoutUrl,
    string QrCode,
    string? Bin,
    string? AccountNumber,
    string? AccountName,
    string Description);

/// <summary>Trạng thái link khi hỏi payOS (lớp 2). Status: PENDING, PAID, CANCELLED, EXPIRED, PROCESSING...</summary>
public sealed record PayOsPaymentInfo(long OrderCode, string Status, decimal Amount, decimal AmountPaid, string? Reference);

/// <summary>Nội dung webhook sau khi xác thực chữ ký.</summary>
public sealed record PayOsWebhook(bool SignatureValid, bool Success, long? OrderCode, decimal? Amount, string? Reference, string? Description);

public interface IPayOsClient
{
    bool IsConfigured { get; }

    Task<PayOsLink> CreatePaymentLinkAsync(long orderCode, decimal amount, string description, DateTime expiresAtUtc, CancellationToken cancellationToken = default);

    Task<PayOsPaymentInfo?> GetPaymentInfoAsync(long orderCode, CancellationToken cancellationToken = default);

    PayOsWebhook VerifyWebhook(string rawPayload);
}

/// <summary>
/// payOS merchant API v2 (https://payos.vn/docs/api/), rút gọn từ PayOsGateway của StreetBiz
/// cho trường hợp một người nhận duy nhất (cơ sở):
/// - POST /v2/payment-requests: ký HMAC-SHA256 (hex) trên "amount=..&amp;cancelUrl=..&amp;description=..&amp;orderCode=..&amp;returnUrl=..".
/// - Webhook ký trên object data: key sắp xếp, "key=value" nối bằng "&amp;", null → "".
/// - GET /v2/payment-requests/{orderCode}: trạng thái link.
/// Local không cần tiền thật: chạy scripts/payos-fake (port 5199) và đặt PayOs:ApiBaseUrl = http://localhost:5199.
/// </summary>
public sealed class PayOsClient(HttpClient http, IOptions<PayOsOptions> options) : IPayOsClient
{
    public const string HttpClientName = "payos";

    private readonly PayOsOptions _options = options.Value;

    public bool IsConfigured => _options.IsConfigured;

    public async Task<PayOsLink> CreatePaymentLinkAsync(long orderCode, decimal amount, string description, DateTime expiresAtUtc, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        long whole = (long)decimal.Round(amount, 0, MidpointRounding.AwayFromZero);
        string signature = Sign(_options.ChecksumKey,
            $"amount={whole}&cancelUrl={_options.CancelUrl}&description={description}&orderCode={orderCode}&returnUrl={_options.ReturnUrl}");

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.ApiBaseUrl.TrimEnd('/')}/v2/payment-requests")
        {
            Content = JsonContent.Create(new
            {
                orderCode,
                amount = whole,
                description,
                cancelUrl = _options.CancelUrl,
                returnUrl = _options.ReturnUrl,
                expiredAt = new DateTimeOffset(expiresAtUtc).ToUnixTimeSeconds(),
                signature
            })
        };
        Authenticate(request);
        var data = await SendAsync(request, "tạo mã thanh toán", cancellationToken);
        string? qr = Str(data, "qrCode");
        if (string.IsNullOrWhiteSpace(qr))
        {
            throw new ServiceUnavailableException("payOS không trả về mã QR. Vui lòng thử lại.");
        }

        return new PayOsLink(
            Str(data, "paymentLinkId"),
            Str(data, "checkoutUrl") ?? "",
            qr,
            Str(data, "bin"),
            Str(data, "accountNumber"),
            Str(data, "accountName"),
            Str(data, "description") ?? description);
    }

    public async Task<PayOsPaymentInfo?> GetPaymentInfoAsync(long orderCode, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return null;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"{_options.ApiBaseUrl.TrimEnd('/')}/v2/payment-requests/{orderCode.ToString(CultureInfo.InvariantCulture)}");
        Authenticate(request);
        JsonElement data;
        try
        {
            data = await SendAsync(request, "hỏi trạng thái", cancellationToken);
        }
        catch (ServiceUnavailableException)
        {
            // payOS bận/lỗi tạm thời: coi như chưa có kết quả, lượt đồng bộ sau hỏi lại.
            return null;
        }

        string? reference = null;
        if (data.TryGetProperty("transactions", out var transactions) && transactions.ValueKind == JsonValueKind.Array)
        {
            reference = transactions.EnumerateArray().Select(item => Str(item, "reference")).FirstOrDefault(value => value is not null);
        }

        return new PayOsPaymentInfo(
            orderCode,
            (Str(data, "status") ?? "").ToUpperInvariant(),
            Dec(data, "amount") ?? 0,
            Dec(data, "amountPaid") ?? 0,
            reference);
    }

    public PayOsWebhook VerifyWebhook(string rawPayload)
    {
        try
        {
            using var json = JsonDocument.Parse(rawPayload);
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
            {
                return new PayOsWebhook(false, false, null, null, null, null);
            }

            string? supplied = Str(root, "signature");
            bool valid = !string.IsNullOrWhiteSpace(_options.ChecksumKey) && !string.IsNullOrWhiteSpace(supplied) &&
                HexEquals(Sign(_options.ChecksumKey, SignedText(data)), supplied);
            bool success = Str(root, "code") == "00" && (Str(data, "code") ?? "00") == "00";
            return new PayOsWebhook(valid, success, Long(data, "orderCode"), Dec(data, "amount"), Str(data, "reference"), Str(data, "description"));
        }
        catch (JsonException)
        {
            return new PayOsWebhook(false, false, null, null, null, null);
        }
    }

    // ------------------------------------------------------------------ chữ ký (public để test)

    public static string Sign(string checksumKey, string text) =>
        Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(checksumKey), Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    /// <summary>Chuỗi payOS ký: key của data sắp xếp, "key=value" nối bằng "&amp;".</summary>
    public static string SignedText(JsonElement data)
    {
        var parts = data.EnumerateObject()
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .Select(property => $"{property.Name}={ValueText(property.Value)}");
        return string.Join('&', parts);
    }

    private static string ValueText(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null or JsonValueKind.Undefined => "",
        JsonValueKind.String => value.GetString() is "null" or "undefined" ? "" : value.GetString() ?? "",
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Array or JsonValueKind.Object => SortedJson(JsonNode.Parse(value.GetRawText()))?.ToJsonString() ?? "null",
        _ => value.GetRawText()
    };

    private static JsonNode? SortedJson(JsonNode? node) => node switch
    {
        JsonObject obj => new JsonObject(obj.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => KeyValuePair.Create(pair.Key, SortedJson(pair.Value?.DeepClone())))),
        JsonArray array => new JsonArray(array.Select(item => SortedJson(item?.DeepClone())).ToArray()),
        _ => node?.DeepClone()
    };

    private static bool HexEquals(string expected, string supplied) =>
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(supplied.Trim().ToLowerInvariant()));

    // ------------------------------------------------------------------ HTTP

    private void EnsureConfigured()
    {
        if (!IsConfigured)
        {
            throw new ServiceUnavailableException("Thanh toán PayOS chưa được cấu hình (PayOs:ClientId/ApiKey/ChecksumKey).");
        }
    }

    private void Authenticate(HttpRequestMessage request)
    {
        request.Headers.Add("x-client-id", _options.ClientId);
        request.Headers.Add("x-api-key", _options.ApiKey);
    }

    private async Task<JsonElement> SendAsync(HttpRequestMessage request, string action, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException)
        {
            throw new ServiceUnavailableException($"Không kết nối được payOS khi {action}.");
        }

        using (response)
        {
            string raw = await response.Content.ReadAsStringAsync(cancellationToken);
            JsonElement root;
            try
            {
                root = JsonDocument.Parse(raw).RootElement.Clone();
            }
            catch (JsonException)
            {
                throw new ServiceUnavailableException($"payOS trả về phản hồi không hợp lệ khi {action} (HTTP {(int)response.StatusCode}).");
            }

            if (!response.IsSuccessStatusCode || Str(root, "code") != "00")
            {
                throw new ServiceUnavailableException($"payOS từ chối {action}: {Str(root, "desc") ?? $"HTTP {(int)response.StatusCode}"}");
            }

            return root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object
                ? data.Clone()
                : throw new ServiceUnavailableException($"payOS không trả về dữ liệu khi {action}.");
        }
    }

    private static string? Str(JsonElement root, string name) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.GetRawText(),
                _ => null
            }
            : null;

    private static long? Long(JsonElement root, string name) =>
        long.TryParse(Str(root, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out long value) ? value : null;

    private static decimal? Dec(JsonElement root, string name) =>
        decimal.TryParse(Str(root, name), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value) ? value : null;
}
