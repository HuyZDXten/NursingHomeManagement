using System.Text.Json;
using NursingHome.BusinessObjects.Exceptions;
using NursingHome.Services.Ai.Tools;

namespace NursingHome.Tests;

public class FamilyToolsTests
{
    [Fact]
    public async Task GuardAsync_ConvertsForbiddenAndNotFoundToErrorJson()
    {
        string forbidden = await FamilyTools.GuardAsync(() => throw new ForbiddenException("Không có quyền xem."));
        using (var doc = JsonDocument.Parse(forbidden))
        {
            Assert.Equal("Không có quyền xem.", doc.RootElement.GetProperty("error").GetString());
        }

        string notFound = await FamilyTools.GuardAsync(() => throw new NotFoundException("Không tìm thấy cư dân."));
        using (var doc = JsonDocument.Parse(notFound))
        {
            Assert.Equal("Không tìm thấy cư dân.", doc.RootElement.GetProperty("error").GetString());
        }
    }
}
