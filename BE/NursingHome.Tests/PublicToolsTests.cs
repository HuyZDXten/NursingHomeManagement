using System.Text.Json;
using NSubstitute;
using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Exceptions;
using NursingHome.DTOs.Admissions;
using NursingHome.Services.Ai.Tools;
using NursingHome.Services.Interfaces;

namespace NursingHome.Tests;

/// <summary>S4-V1: tool ghi dữ liệu của chatbot công khai chỉ chạy khi model gửi confirmed=true VÀ khách vừa xác nhận.</summary>
public class PublicToolsTests
{
    private static JsonElement Args(object value) => JsonSerializer.SerializeToElement(value);

    [Theory]
    [InlineData("Đồng ý", true)]
    [InlineData("ok em", true)]
    [InlineData("Vâng, xác nhận giúp tôi", true)]
    [InlineData("Có giường trống không?", false)]
    [InlineData("Được không ạ?", false)]
    [InlineData("Tôi muốn được tư vấn", false)]
    public void UserConfirmed_OnlyForClearAffirmations(string message, bool expected) =>
        Assert.Equal(expected, new PublicChatContext(message).UserConfirmed);

    [Fact]
    public async Task CreateInquiry_WithoutUserConfirmation_DoesNotWrite()
    {
        var admissions = Substitute.For<IAdmissionService>();
        var tool = new CreateInquiryTool(new PublicChatContext("Tôi muốn được tư vấn"), admissions);

        var output = await tool.ExecuteAsync(null, Args(new { fullName = "Khách", phone = "0987654321", confirmed = true }));

        Assert.Contains("needsConfirmation", output);
        await admissions.DidNotReceive().CreateInquiryAsync(Arg.Any<CreateInquiryRequest>(), Arg.Any<InquirySource>());
    }

    [Fact]
    public async Task CreateInquiry_Confirmed_UsesChatbotSource()
    {
        var admissions = Substitute.For<IAdmissionService>();
        admissions.CreateInquiryAsync(Arg.Any<CreateInquiryRequest>(), InquirySource.Chatbot)
            .Returns(new InquiryDto { Id = 9, Phone = "0987654321" });
        var tool = new CreateInquiryTool(new PublicChatContext("Đồng ý"), admissions);

        var output = await tool.ExecuteAsync(null, Args(new { fullName = "Khách", phone = "0987654321", confirmed = true }));

        Assert.Contains("\"created\":true", output);
        await admissions.Received(1).CreateInquiryAsync(Arg.Is<CreateInquiryRequest>(r => r.FullName == "Khách"), InquirySource.Chatbot);
    }

    [Fact]
    public async Task CreateInquiry_InvalidPhone_IsRejected()
    {
        var tool = new CreateInquiryTool(new PublicChatContext("Đồng ý"), Substitute.For<IAdmissionService>());
        await Assert.ThrowsAsync<BusinessRuleException>(() => tool.ExecuteAsync(null, Args(new { fullName = "", phone = "", confirmed = true })));
    }

    [Fact]
    public void Keywords_DropStopWordsAndPunctuation()
    {
        var words = SearchKnowledgeTool.Keywords("Cho tôi hỏi giờ thăm của cơ sở thế nào ạ?");
        Assert.Contains("giờ", words);
        Assert.Contains("thăm", words);
        Assert.DoesNotContain("tôi", words);
        Assert.DoesNotContain("ạ", words);
    }
}
