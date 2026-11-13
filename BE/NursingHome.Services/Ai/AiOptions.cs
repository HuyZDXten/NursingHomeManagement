namespace NursingHome.Services.Ai;

/// <summary>
/// Section "Ai" trong appsettings.json. Key thật chỉ đặt ở user-secrets: <c>dotnet user-secrets set "Ai:Gemini:ApiKey" "..."</c>.
/// <list type="bullet">
/// <item><c>Enabled = false</c>: tắt mọi tính năng AI (widget ẩn, endpoint trả 503).</item>
/// <item><c>Provider = "Fake"</c>: trả lời theo kịch bản cố định, không gọi mạng (dev/test/demo không có key).</item>
/// <item><c>Provider = "Gemini"</c>: cần <c>Gemini:ApiKey</c> và <c>Gemini:Model</c> (StreetBiz dùng "gemini-3.8-flash").</item>
/// </list>
/// </summary>
public class AiOptions
{
    public const string SectionName = "Ai";
    public const string FakeProvider = "Fake";
    public const string GeminiProvider = "Gemini";

    public bool Enabled { get; set; }
    public string Provider { get; set; } = GeminiProvider;
    public GeminiOptions Gemini { get; set; } = new();

    /// <summary>Dự phòng kiểu OpenAI-compatible (chưa dùng ở bản này; giữ chỗ cấu hình như StreetBiz).</summary>
    public FallbackOptions Fallback { get; set; } = new();
    public int MaxOutputTokens { get; set; } = 1024;
    public double Temperature { get; set; } = 0.3;
    public bool PublicChatbotEnabled { get; set; } = true;

    /// <summary>Hết thời gian gọi provider thì trả câu xin lỗi.</summary>
    public int TimeoutSeconds { get; set; } = 15;

    public bool UsesFake => string.Equals(Provider, FakeProvider, StringComparison.OrdinalIgnoreCase);
}

public class GeminiOptions
{
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "";
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta";
}

public class FallbackOptions
{
    public string BaseUrl { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "";
}
