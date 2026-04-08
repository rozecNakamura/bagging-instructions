using BaggingInstructions.Api.Services;

namespace BaggingInstructions.Api.Tests;

public class AggregationRuleServiceTests
{
    [Theory]
    [InlineData(null, "昼食", "lunch")]
    [InlineData("09", null, "morning")]
    [InlineData("12", null, "lunch")]
    [InlineData("18", null, "dinner")]
    [InlineData("朝", null, "morning")]
    public void ResolveMealPeriodKey_MapsNameOrHour(string? code, string? name, string expectedKey)
    {
        var k = AggregationRuleService.ResolveMealPeriodKey(code, name);
        Assert.Equal(expectedKey, k);
    }

    [Theory]
    [InlineData("200", "09", null, "by_facility")]
    [InlineData("200", "12", null, "by_catering")]
    [InlineData("000", "12", null, "by_facility")]
    public void GetAggregationMethod_UsesResolvedMeal(string cuscd, string? code, string? name, string expected)
    {
        Assert.Equal(expected, AggregationRuleService.GetAggregationMethod(cuscd, code, name));
    }
}
