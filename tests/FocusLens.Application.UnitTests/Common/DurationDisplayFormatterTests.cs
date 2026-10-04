using FocusLens.Application.Common.Utilities;

namespace FocusLens.Application.UnitTests.Common;

public sealed class DurationDisplayFormatterTests
{
    [Theory]
    [InlineData(0, "0 minutes")]
    [InlineData(20, "20 minutes")]
    [InlineData(30, "30 minutes")]
    [InlineData(45, "45 minutes")]
    [InlineData(60, "1 hour")]
    [InlineData(61, "1 hour 1 minute")]
    [InlineData(75, "1 hour 15 minutes")]
    [InlineData(90, "1 hour 30 minutes")]
    [InlineData(120, "2 hours")]
    [InlineData(125, "2 hours 5 minutes")]
    [InlineData(150, "2 hours 30 minutes")]
    [InlineData(467, "7 hours 47 minutes")]
    [InlineData(1830, "30 hours 30 minutes")]
    [InlineData(2130, "35 hours 30 minutes")]
    public void FormatMinutes_UsesWholeHoursAndRemainingMinutes(int minutes, string expected)
    {
        string actual = DurationDisplayFormatter.FormatMinutes(minutes);
        Assert.Equal(expected, actual);
        Assert.DoesNotContain(".5 hours", actual);
    }

    [Fact]
    public void FormatMinutes_FormatsNegativeAnalyticsWithoutDecimalHours() =>
        Assert.Equal("-1 hour 15 minutes", DurationDisplayFormatter.FormatMinutes(-75));
}
