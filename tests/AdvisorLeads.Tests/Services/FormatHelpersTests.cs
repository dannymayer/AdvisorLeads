using AdvisorLeads.Services;

namespace AdvisorLeads.Tests.Services;

/// <summary>
/// Tests for shared utility/helper services.
/// </summary>
public class FormatHelpersTests
{
    [Theory]
    [InlineData(1_500_000_000, "$1.5B")]
    [InlineData(1_000_000_000, "$1.0B")]
    [InlineData(500_000_000, "$500.0M")]
    [InlineData(1_000_000, "$1.0M")]
    [InlineData(750_000, "$750K")]
    [InlineData(1_000, "$1K")]
    [InlineData(500, "$500")]
    [InlineData(0, "$0")]
    public void FormatAum_FormatsCorrectly(decimal input, string expected)
    {
        var result = FormatHelpers.FormatAum(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void FormatAum_NegativeValue_ReturnsStringRepresentation()
    {
        // Negative AUM falls through to the default format
        var result = FormatHelpers.FormatAum(-500);
        Assert.StartsWith("$", result);
    }
}
