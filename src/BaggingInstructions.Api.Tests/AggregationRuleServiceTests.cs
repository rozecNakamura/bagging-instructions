using BaggingInstructions.Api.Services;

namespace BaggingInstructions.Api.Tests;

public class AggregationRuleServiceTests
{
    [Theory]
    [InlineData("200", "1", "by_facility")]   // 朝 → by_facility
    [InlineData("200", "2", "by_catering")]   // 昼 → by_catering
    [InlineData("200", "3", "by_catering")]   // 夜 → by_catering
    [InlineData("000", "2", "by_facility")]   // 昼でも000は by_facility
    [InlineData("300", "1", "by_facility")]   // 300 朝はnull → by_facility
    [InlineData("300", "2", "by_catering")]   // 300 昼 → by_catering
    [InlineData("200", null, "by_catering")]  // null → lunch扱い → by_catering
    [InlineData(null, "2", "by_facility")]    // 未定義cuscd → by_facility
    public void GetAggregationMethod_UsesAddinfo05(string? cuscd, string? addinfo05, string expected)
    {
        Assert.Equal(expected, AggregationRuleService.GetAggregationMethod(cuscd, addinfo05));
    }
}
