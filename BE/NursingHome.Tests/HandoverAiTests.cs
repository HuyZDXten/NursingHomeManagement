using NursingHome.DTOs.Staffing;
using NursingHome.Services.Ai;

namespace NursingHome.Tests;

/// <summary>S4-V2: bản tóm tắt bàn giao theo quy tắc (provider Fake hoặc khi AI lỗi).</summary>
public class HandoverAiTests
{
    [Fact]
    public void RuleBasedSummary_GroupsByResident_PutsUrgentFirst()
    {
        var summary = HandoverAiService.RuleBasedSummary(
        [
            new HandoverItemDto { Kind = "Task", ResidentName = "NH-0001", Text = "Chưa xong: đo sinh hiệu." },
            new HandoverItemDto { Kind = "Medication", ResidentName = "NH-0002", Text = "Thuốc A bị từ chối." },
            new HandoverItemDto { Kind = "Task", ResidentName = "NH-0002", Text = "Chưa xong: tập đi." }
        ]);

        var lines = summary.Split('\n');
        Assert.Equal(2, lines.Length);
        Assert.StartsWith("- [ƯU TIÊN] NH-0002: Thuốc A bị từ chối.", lines[0]);
        Assert.Contains("Chưa xong: tập đi.", lines[0]);
        Assert.Equal("- NH-0001: Chưa xong: đo sinh hiệu.", lines[1]);
    }

    [Fact]
    public void RuleBasedSummary_KeepsAtMostTwelveLines()
    {
        var items = Enumerable.Range(1, 20)
            .Select(i => new HandoverItemDto { Kind = "Task", ResidentName = $"NH-{i:0000}", Text = "Chưa xong." })
            .ToList();
        Assert.Equal(HandoverAiService.MaxLines, HandoverAiService.RuleBasedSummary(items).Split('\n').Length);
    }
}
