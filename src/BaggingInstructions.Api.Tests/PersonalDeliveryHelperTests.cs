using BaggingInstructions.Api.Entities;
using BaggingInstructions.Api.Services;

namespace BaggingInstructions.Api.Tests;

public class PersonalDeliveryHelperTests
{
    [Theory]
    [InlineData("1", "C01", "O01", "C01", "O01")]
    [InlineData("2", "C01", "O01", "C03", "O04")]
    [InlineData("3", "C01", "O01", "C05", "O06")]
    public void ResolveCourseAndOrder_uses_info19_mapping(
        string info19,
        string addinfo01,
        string addinfo02,
        string expectedCourse,
        string expectedOrder)
    {
        var addinfo = new CustomerDeliveryLocationAddinfo
        {
            Addinfo01 = addinfo01,
            Addinfo02 = addinfo02,
            Addinfo03 = "C03",
            Addinfo04 = "O04",
            Addinfo05 = "C05",
            Addinfo06 = "O06"
        };

        var (course, order) = PersonalDeliveryHelper.ResolveCourseAndOrder(info19, addinfo);

        Assert.Equal(expectedCourse, course);
        Assert.Equal(expectedOrder, order);
    }

    [Theory]
    [InlineData("3010001", true)]
    [InlineData("3011001", true)]
    [InlineData("3111001", true)]
    [InlineData("3411001", true)]
    [InlineData("4011001", false)]
    public void IsRiceItemCode_checks_prefix(string itemCode, bool expected) =>
        Assert.Equal(expected, PersonalDeliveryHelper.IsRiceItemCode(itemCode));

    [Theory]
    [InlineData("3011001", PersonalDeliveryHelper.SummaryItemCategory.StapleFood)]
    [InlineData("305001", PersonalDeliveryHelper.SummaryItemCategory.Soup)]
    [InlineData("4011001", PersonalDeliveryHelper.SummaryItemCategory.MainDish)]
    [InlineData("3111001", PersonalDeliveryHelper.SummaryItemCategory.MainDish)]
    public void ResolveSummaryItemCategory_classifies_items(string itemCode, PersonalDeliveryHelper.SummaryItemCategory expected) =>
        Assert.Equal(expected, PersonalDeliveryHelper.ResolveSummaryItemCategory(itemCode));

    [Theory]
    [InlineData("300", true)]
    [InlineData("310", true)]
    [InlineData("240", false)]
    public void IsSummaryTargetCustomer_accepts_300_and_310(string customerCode, bool expected) =>
        Assert.Equal(expected, PersonalDeliveryHelper.IsSummaryTargetCustomer(customerCode));

    [Theory]
    [InlineData("関東", "関東")]
    [InlineData("01 関東", "関東")]
    [InlineData("01  関東", "関東")]
    [InlineData("01 関東 北", "関東 北")]
    [InlineData("01\n関東", "関東")]
    [InlineData("01\r\n関東", "関東")]
    [InlineData("", "")]
    public void FormatDeliveryAreaDisplay_returns_text_after_space_separator(string input, string expected) =>
        Assert.Equal(expected, PersonalDeliveryHelper.FormatDeliveryAreaDisplay(input));
}
