using System.Globalization;

namespace NursingHome.Services.Helpers;

/// <summary>S3-H3: tách chuỗi giờ thăm (vd. "08:00-11:00, 14:00-20:00") thành các khung 1 tiếng.</summary>
public static class VisitingHours
{
    public static IReadOnlyList<(TimeOnly Start, TimeOnly End)> Slots(string? config)
    {
        if (string.IsNullOrWhiteSpace(config))
        {
            return [];
        }

        var result = new List<(TimeOnly Start, TimeOnly End)>();
        foreach (string rawPart in config.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string[] bounds = rawPart.Split('-', StringSplitOptions.TrimEntries);
            if (bounds.Length != 2)
            {
                continue;
            }

            if (!TimeOnly.TryParseExact(bounds[0], "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start) ||
                !TimeOnly.TryParseExact(bounds[1], "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var end))
            {
                continue;
            }

            for (var cursor = start; cursor.AddHours(1) <= end && cursor.AddHours(1) > cursor; cursor = cursor.AddHours(1))
            {
                result.Add((cursor, cursor.AddHours(1)));
            }
        }

        return result;
    }
}
