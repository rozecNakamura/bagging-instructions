using BaggingInstructions.Api.Services;

namespace BaggingInstructions.Api.Tests;

public class PersonalDeliveryPdfServiceTests
{
    [Theory]
    [InlineData("CUSTOMERNM00", true)]
    [InlineData("CUSTOMERLOC12", true)]
    [InlineData("FOODTYPE05", true)]
    [InlineData("RICETYPE03", true)]
    [InlineData("GRAM07", true)]
    [InlineData("NOTE01", true)]
    [InlineData("ORDER02", true)]
    [InlineData("COUNT04", true)]
    [InlineData("DATE", true)]
    [InlineData("TIME", true)]
    [InlineData("AREA", true)]
    [InlineData("PAGECOUNT", true)]
    [InlineData("PRINTDATE", true)]
    [InlineData("PRINTTIME", true)]
    [InlineData("LABEL", false)]
    public void ShouldApplyTextLayout_matches_personal_delivery_data_fields(string fieldName, bool expected) =>
        Assert.Equal(expected, PersonalDeliveryPdfService.ShouldApplyTextLayout(fieldName));
}
