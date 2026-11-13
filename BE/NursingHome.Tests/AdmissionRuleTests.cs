using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Exceptions;
using NursingHome.Services.Implementations;

namespace NursingHome.Tests;

/// <summary>S3-V1: chuẩn hóa SĐT và các bước hợp lệ của phễu khách (không cần DB).</summary>
public class AdmissionRuleTests
{
    [Theory]
    [InlineData("090 123 4567", "0901234567")]
    [InlineData("0901.234.567", "0901234567")]
    [InlineData("(+84) 901-234-567", "+84901234567")]
    public void NormalizePhone_RemovesSeparators(string input, string expected) =>
        Assert.Equal(expected, AdmissionService.NormalizePhone(input));

    [Theory]
    [InlineData("12345")]
    [InlineData("09012abc67")]
    [InlineData("")]
    public void NormalizePhone_RejectsInvalid(string input) =>
        Assert.Throws<BusinessRuleException>(() => AdmissionService.NormalizePhone(input));

    [Theory]
    [InlineData(InquiryStatus.New, InquiryStatus.Contacted, true)]
    [InlineData(InquiryStatus.New, InquiryStatus.Visited, true)]
    [InlineData(InquiryStatus.New, InquiryStatus.Lost, true)]
    [InlineData(InquiryStatus.Contacted, InquiryStatus.Visited, true)]
    [InlineData(InquiryStatus.Contacted, InquiryStatus.Lost, true)]
    [InlineData(InquiryStatus.Visited, InquiryStatus.Admitted, true)]
    [InlineData(InquiryStatus.Visited, InquiryStatus.Lost, true)]
    [InlineData(InquiryStatus.New, InquiryStatus.Admitted, false)]
    [InlineData(InquiryStatus.Contacted, InquiryStatus.Admitted, false)]
    [InlineData(InquiryStatus.Visited, InquiryStatus.New, false)]
    [InlineData(InquiryStatus.Admitted, InquiryStatus.Lost, false)]
    [InlineData(InquiryStatus.Lost, InquiryStatus.Contacted, false)]
    public void CanTransition_FollowsFunnel(InquiryStatus from, InquiryStatus to, bool expected) =>
        Assert.Equal(expected, AdmissionService.CanTransition(from, to));
}
