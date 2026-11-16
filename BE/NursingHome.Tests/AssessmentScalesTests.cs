using NursingHome.BusinessObjects.Entities;
using NursingHome.Services.Helpers;

namespace NursingHome.Tests;

public class AssessmentScalesTests
{
    [Fact]
    public void Score_FallRisk_ComputesLowMediumHighCorrectly()
    {
        var low = AssessmentScales.Score(AssessmentType.FallRisk, new Dictionary<string, string>
        {
            ["history"] = "no", ["secondary"] = "no", ["aid"] = "none", ["iv"] = "no", ["gait"] = "normal", ["mental"] = "oriented"
        });
        Assert.Equal((0, 125, RiskLevel.Low), low);

        var medium = AssessmentScales.Score(AssessmentType.FallRisk, new Dictionary<string, string>
        {
            ["history"] = "yes", ["secondary"] = "no", ["aid"] = "none", ["iv"] = "no", ["gait"] = "weak", ["mental"] = "oriented"
        });
        Assert.Equal((35, 125, RiskLevel.Medium), medium);

        var high = AssessmentScales.Score(AssessmentType.FallRisk, new Dictionary<string, string>
        {
            ["history"] = "yes", ["secondary"] = "yes", ["aid"] = "crutch", ["iv"] = "no", ["gait"] = "normal", ["mental"] = "oriented"
        });
        Assert.Equal((55, 125, RiskLevel.High), high);
    }
}
