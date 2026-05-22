using BaggingInstructions.Api.Services;

namespace BaggingInstructions.Api.Tests;

public class BaggingEatingTimeLabelTests
{
    [Theory]
    [InlineData("1", "朝")]
    [InlineData("2", "昼")]
    [InlineData("3", "夕")]
    [InlineData(" 1 ", "朝")]
    [InlineData("01", "朝")]
    [InlineData("02", "昼")]
    [InlineData("03", "夕")]
    [InlineData("\uFF11", "朝")]
    public void MapFromAddinfo05_known_codes(string raw, string expected) =>
        Assert.Equal(expected, BaggingEatingTimeLabel.MapFromAddinfo05(raw));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("4")]
    [InlineData("朝")]
    public void MapFromAddinfo05_unknown_returns_empty(string? raw) =>
        Assert.Equal("", BaggingEatingTimeLabel.MapFromAddinfo05(raw));
}
