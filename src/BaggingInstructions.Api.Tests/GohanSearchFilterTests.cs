using BaggingInstructions.Api.Services;

namespace BaggingInstructions.Api.Tests;

public class GohanSearchFilterTests
{
    [Theory]
    [InlineData("30110001", true)]
    [InlineData("31110001", true)]
    [InlineData("34110001", true)]
    [InlineData("30120001", false)]
    [InlineData("30500001", false)]
    [InlineData("", false)]
    public void IsTargetItemCode_checks_first_four_digits(string itemCode, bool expected) =>
        Assert.Equal(expected, GohanSearchFilter.IsTargetItemCode(itemCode));

    [Theory]
    [InlineData("0", new[] { "200", "210" })]
    [InlineData("1", new[] { "240", "300", "310" })]
    [InlineData("", new[] { "200", "210", "240", "300", "310" })]
    public void AllowedCustomerCodes_returns_codes_by_type(string addinfo08Type, string[] expected) =>
        Assert.Equal(expected, GohanSearchFilter.AllowedCustomerCodes(addinfo08Type));

    [Theory]
    [InlineData("200", "0", true)]
    [InlineData("210", "0", true)]
    [InlineData("240", "0", false)]
    [InlineData("240", "1", true)]
    [InlineData("300", "1", true)]
    [InlineData("310", "1", true)]
    [InlineData("200", "1", false)]
    [InlineData("0200", "0", true)]
    [InlineData("200", "", true)]
    [InlineData("240", "", true)]
    public void IsTargetCustomer_matches_normalized_customer_code(string customerCode, string addinfo08Type, bool expected) =>
        Assert.Equal(expected, GohanSearchFilter.IsTargetCustomer(customerCode, addinfo08Type));
}
