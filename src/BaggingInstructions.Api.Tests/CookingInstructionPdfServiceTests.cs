using BaggingInstructions.Api.DTOs;
using BaggingInstructions.Api.Services;

namespace BaggingInstructions.Api.Tests;

public class CookingInstructionPdfServiceTests
{
    [Fact]
    public void BuildPageTagValues_maps_header_and_first_row_correctly()
    {
        var line = new CookingInstructionPdfLineModel
        {
            OrderNo = "ORD-001",
            ParentItemCode = "P001",
            ParentItemName = "親商品",
            PlannedQuantityDisplay = "10.5",
            PlanUnitName = "kg",
            ChildItemCode = "C001",
            ChildItemName = "子商品",
            ChildRequiredQtyDisplay = "2.5",
            ChildUnitName = "g",
            NeedDateDisplay = "2025/03/01",
            SlotDisplay = "朝便",
            WorkplaceNames = "調理場A"
        };

        var tags = CookingInstructionPdfService.BuildPageTagValues(
            new[] { line },
            line.SlotDisplay,
            line.NeedDateDisplay);

        Assert.Equal("調理場A", tags["GENRE01"]);
        Assert.Equal("2025/03/01", tags["DATE01"]);
        Assert.Equal("朝便", tags["ITEMTYPE01"]);

        Assert.Equal("P001 親商品", tags["ITEMPALNM00"]);
        Assert.Equal("C001 子商品", tags["ITEMCHINM00"]);
        Assert.Equal("10.5", tags["ITEMPALNUM00"]);
        Assert.Equal("2.5", tags["ITEMCHINUM00"]);
        Assert.Equal("kg", tags["UNITPAR00"]);
        Assert.Equal("g", tags["UNITCHI00"]);
        Assert.Equal("ORD-001", tags["ORDERNO00"]);
    }

    [Theory]
    [InlineData("A", "B", "A B")]
    [InlineData("", "B", "B")]
    [InlineData("A", "", "A")]
    [InlineData("  ", "  ", "")]
    public void FormatItemCodeName_joins_code_and_name(string code, string name, string expected)
    {
        Assert.Equal(expected, CookingInstructionPdfService.FormatItemCodeName(code, name));
    }
}

