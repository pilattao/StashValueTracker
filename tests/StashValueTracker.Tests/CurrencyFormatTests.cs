using StashValueTracker.Formatting;
using Xunit;

namespace StashValueTracker.Tests;

public class CurrencyFormatTests
{
    [Theory]
    [InlineData(1240, "1 240")]
    [InlineData(50000, "50 000")]
    [InlineData(1135, "1 135")]
    [InlineData(100, "100")]
    [InlineData(42, "42")]
    [InlineData(8.68, "8.7")]
    [InlineData(1, "1")]
    [InlineData(0.3, "0.3")]
    [InlineData(0.04, "0.04")]
    [InlineData(0, "0")]
    [InlineData(-5, "-5")]
    public void FormatNumber_uses_scaled_precision(double value, string expected)
    {
        Assert.Equal(expected, CurrencyFormat.FormatNumber(value));
    }

    [Fact]
    public void ExWithDiv_appends_divine_suffix()
    {
        Assert.Equal("1 240 ex (~8.7 div)", CurrencyFormat.ExWithDiv(1240, 0.007));
        Assert.Equal("900 ex (~6.3 div)", CurrencyFormat.ExWithDiv(900, 0.007));
    }

    [Fact]
    public void ExWithDiv_handles_small_divine_values()
    {
        Assert.Equal("42 ex (~0.29 div)", CurrencyFormat.ExWithDiv(42, 0.007));
    }

    [Fact]
    public void ExWithDiv_omits_divine_when_ratio_unknown()
    {
        Assert.Equal("42 ex", CurrencyFormat.ExWithDiv(42, 0));
    }

    [Theory]
    [InlineData(200, 0.01, "2 div")]     // 2.0 divine → divine
    [InlineData(50, 0.01, "50 ex")]      // 0.5 divine → exalted
    [InlineData(142, 0.007, "142 ex")]   // 0.994 divine → exalted
    [InlineData(100, 0, "100 ex")]       // unknown rate → exalted
    public void Auto_uses_divine_only_when_at_least_one(double ex, double ratio, string expected)
    {
        Assert.Equal(expected, CurrencyFormat.Auto(ex, ratio));
    }

    [Fact]
    public void Tooltip_shows_both_denominations()
    {
        Assert.Equal("2 div · 200 ex", CurrencyFormat.Tooltip(200, 0.01));
    }

    [Fact]
    public void Tooltip_is_exalted_only_when_rate_unknown()
    {
        Assert.Equal("100 ex", CurrencyFormat.Tooltip(100, 0));
    }
}
