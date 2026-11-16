using NursingHome.Services.Helpers;

namespace NursingHome.Tests;

public class TextMatchTests
{
    [Theory]
    [InlineData("Pênicillin", "penicillin")]
    [InlineData("  Đau Dạ Dày  ", "dau da day")]
    [InlineData("Amoxicillin / Pênicillin 500mg", "amoxicillin / penicillin 500mg")]
    [InlineData("Tôm sú", "tom su")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Normalize_RemovesDiacriticsAndLowercases(string? input, string expected)
    {
        Assert.Equal(expected, TextMatch.Normalize(input));
    }

    [Fact]
    public void DrugAllergyMatch_FindsAccentInsensitiveSubstring()
    {
        string med = TextMatch.Normalize("Amoxicillin/Pênicillin 500mg");
        string allergen = TextMatch.Normalize("Penicillin");
        Assert.Contains(allergen, med, StringComparison.Ordinal);
    }
}
