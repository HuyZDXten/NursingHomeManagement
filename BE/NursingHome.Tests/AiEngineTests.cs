using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NursingHome.BusinessObjects.Entities;
using NursingHome.DTOs.Common;
using NursingHome.Repositories.Interfaces;
using NursingHome.Services.Ai;
using NursingHome.Services.Helpers;
using NursingHome.Services.Options;

namespace NursingHome.Tests;

/// <summary>S4-V1: vòng lặp tool của AiChatEngine (provider giả), dựng payload/đọc kết quả Gemini không cần mạng.</summary>
public class AiEngineTests
{
    private readonly IAiRepository repository = Substitute.For<IAiRepository>();
    private List<AiMessageRecord> saved = [];

    public AiEngineTests()
    {
        repository.GetOrCreateConversationAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<DateTime>())
            .Returns(new AiConversation { Id = 7, Channel = AiChannels.Public, AnonymousKey = "k" });
        repository.ListMessagesAsync(7, Arg.Any<int>(), false).Returns([]);
        repository.AddMessagesAsync(7, Arg.Do<IReadOnlyCollection<AiMessageRecord>>(items => saved = items.ToList()), Arg.Any<string?>(), Arg.Any<DateTime>())
            .Returns(Task.CompletedTask);
    }

    private AiChatEngine Engine(IAiChatProvider provider) => new(repository, provider,
        Microsoft.Extensions.Options.Options.Create(new FacilityOptions { Hotline = "0236 000 0000" }),
        new FacilityClock(TimeProvider.System), NullLogger<AiChatEngine>.Instance);

    [Fact]
    public async Task ToolIsCalledWithArguments_AndResultGoesBackToModel()
    {
        var tool = new EchoTool();
        var reply = await Engine(new FakeAiChatProvider()).ChatAsync(AiChannels.Public, null, "k",
            "/tool echo {\"x\": 41}", "system", [tool]);

        Assert.Equal(41, tool.LastX);
        Assert.Equal("x = 41", reply.Text); // Fake đọc trường summary của kết quả tool
        Assert.Equal([AiRoles.User, AiRoles.Tool, AiRoles.Assistant], saved.Select(item => item.Role));
        Assert.Equal("echo", saved[1].ToolName);
    }

    [Fact]
    public async Task StopsOfferingToolsAfterThreeRounds()
    {
        var provider = Substitute.For<IAiChatProvider>();
        var offered = new List<int>();
        provider.CompleteAsync(Arg.Any<AiChatRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var request = call.Arg<AiChatRequest>();
            offered.Add(request.Tools.Count);
            return request.Tools.Count > 0
                ? new AiChatResult(null, [new AiToolCall("local-1", "echo", JsonDocument.Parse("{\"x\":1}").RootElement)], 10, 2)
                : new AiChatResult("Hết vòng", [], 5, 3);
        });

        var reply = await Engine(provider).ChatAsync(AiChannels.Public, null, "k", "hỏi", "system", [new EchoTool()]);

        Assert.Equal("Hết vòng", reply.Text);
        Assert.Equal([1, 1, 1, 0], offered);
        Assert.Equal(3, saved.Count(item => item.Role == AiRoles.Tool));
        Assert.Equal(35, saved.Last().InputTokens);
    }

    [Fact]
    public async Task ProviderFailure_ReturnsApologyWithHotline_AndStillSaves()
    {
        var provider = Substitute.For<IAiChatProvider>();
        provider.CompleteAsync(Arg.Any<AiChatRequest>(), Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException("down"));

        var reply = await Engine(provider).ChatAsync(AiChannels.Public, null, "k", "giá gói?", "system", []);

        Assert.Contains("0236 000 0000", reply.Text);
        Assert.Equal(2, saved.Count);
    }

    [Fact]
    public async Task FakeProvider_RefusesMedicalAdvice()
    {
        var reply = await Engine(new FakeAiChatProvider()).ChatAsync(AiChannels.Public, null, "k",
            "Bà tôi bị tiểu đường dùng thuốc gì?", "system", [new EchoTool()]);
        Assert.Equal(FakeAiChatProvider.MedicalRefusal, reply.Text);
    }

    [Fact]
    public void GeminiPayload_GroupsToolResponses_AndReplaysNativeContent()
    {
        var native = JsonDocument.Parse("""{"role":"model","parts":[{"functionCall":{"name":"echo","args":{"x":1}},"thoughtSignature":"sig"}]}""").RootElement;
        var request = new AiChatRequest("system",
        [
            new AiMessage("user", "hỏi"),
            new AiMessage("assistant", "", Native: native),
            new AiMessage("tool", "{\"summary\":\"ok\"}", "echo", "local-abc")
        ], [new EchoTool().Definition]);

        var payload = GeminiChatProvider.BuildPayload(request, "gemini-3.8-flash", 512, 0.3);
        var contents = payload["contents"]!.AsArray();

        Assert.Equal(3, contents.Count);
        Assert.Equal("sig", contents[1]!["parts"]![0]!["thoughtSignature"]!.GetValue<string>());
        var response = contents[2]!["parts"]![0]!["functionResponse"]!;
        Assert.Equal("echo", response["name"]!.GetValue<string>());
        Assert.Null(response["id"]); // id tự sinh ("local-") không gửi lại
        Assert.Equal("ok", response["response"]!["result"]!["summary"]!.GetValue<string>());
        Assert.NotNull(payload["generationConfig"]!["thinkingConfig"]);
        Assert.Equal("echo", payload["tools"]![0]!["functionDeclarations"]![0]!["name"]!.GetValue<string>());
    }

    [Fact]
    public void GeminiResponse_ReadsCallsAndText_SkipsThoughts()
    {
        var root = JsonDocument.Parse("""
            {"candidates":[{"content":{"role":"model","parts":[
              {"text":"suy nghĩ","thought":true},
              {"text":"Xin chào"},
              {"functionCall":{"id":"call-1","name":"echo","args":{"x":2}}}
            ]},"finishReason":"STOP"}],
             "usageMetadata":{"promptTokenCount":12,"candidatesTokenCount":4}}
            """).RootElement;

        var result = GeminiChatProvider.ParseResponse(root);

        Assert.Equal("Xin chào", result.Text);
        Assert.Equal("call-1", Assert.Single(result.ToolCalls).Id);
        Assert.Equal(2, result.ToolCalls[0].Arguments.GetProperty("x").GetInt32());
        Assert.Equal((12, 4), (result.InputTokens, result.OutputTokens));
        Assert.NotNull(result.Native);
    }

    private sealed class EchoTool : IAiTool
    {
        public int? LastX { get; private set; }

        public AiToolDefinition Definition { get; } = new("echo", "Trả lại x", AiToolJson.Schema(("x", "integer", "số", true)));

        public Task<string> ExecuteAsync(CurrentUser? user, JsonElement arguments, CancellationToken cancellationToken = default)
        {
            LastX = AiToolJson.Int(arguments, "x");
            return Task.FromResult(AiToolJson.Serialize(new { x = LastX, summary = $"x = {LastX}" }));
        }
    }
}
