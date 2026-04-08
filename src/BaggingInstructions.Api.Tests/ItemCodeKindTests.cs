using BaggingInstructions.Api.Services;

namespace BaggingInstructions.Api.Tests;

public class ItemCodeKindTests
{
    [Theory]
    [InlineData("55001", true)]
    [InlineData("551", true)]
    [InlineData("54", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void IsLiquid_MatchesPrefix55(string? code, bool expected)
    {
        Assert.Equal(expected, ItemCodeKind.IsLiquid(code));
    }
}
