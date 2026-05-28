using BaggingInstructions.Api.Services;

namespace BaggingInstructions.Api.Tests;

public class BentoSearchFilterTests
{
    [Theory]
    [InlineData("okazu", false)]
    [InlineData("gohan", true)]
    [InlineData("GOHAN", true)]
    [InlineData(null, false)]
    public void IsGohan_detects_type(string? bentoType, bool expected)
    {
        Assert.Equal(expected, BentoSearchFilter.IsGohan(bentoType));
    }

    [Theory]
    [InlineData("240", true)]
    [InlineData("300", true)]
    [InlineData("310", true)]
    [InlineData("200", false)]
    [InlineData("0240", true)]
    public void IsTargetCustomer_accepts_240_300_310(string customerCode, bool expected)
    {
        Assert.Equal(expected, BentoSearchFilter.IsTargetCustomer(customerCode));
    }

    [Theory]
    [InlineData("1BOX", true)]
    [InlineData("0BOX", false)]
    [InlineData("1", true)]
    public void IsTargetGohanAddinfo08_requires_leading_1(string addinfo08, bool expected)
    {
        Assert.Equal(expected, BentoSearchFilter.IsTargetGohanAddinfo08(addinfo08));
    }

    [Theory]
    [InlineData("30100001", true)]
    [InlineData("30110001", true)]
    [InlineData("31110001", true)]
    [InlineData("34110001", true)]
    [InlineData("40110001", false)]
    public void IsTargetGohanItemCode_checks_prefix(string itemCode, bool expected)
    {
        Assert.Equal(expected, BentoSearchFilter.IsTargetGohanItemCode(itemCode));
    }
}
