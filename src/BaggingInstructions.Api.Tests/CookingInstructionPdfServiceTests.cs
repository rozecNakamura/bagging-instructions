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
            OrderNo = "10042",
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

        Assert.Equal("10042", tags["ITEMPALNM00"]);
        Assert.Equal("P001 親商品", tags["ITEMPALNUM00"]);
        Assert.Equal("10.5", tags["MAKEQUNPLAN00"]);
        Assert.Equal("C001 子商品", tags["ITEMCHINM00"]);
        Assert.Equal("", tags["ITEMCHINUM00"]);
        Assert.Equal("2.5", tags["USEQUNPLAN00"]);
        Assert.Equal("kg", tags["UNITPAR01"]);
        Assert.Equal("g", tags["UNITCHI00"]);
        Assert.Equal("", tags["ORDERNO00"]);
    }

    [Fact]
    public void BuildPageTagValues_unitPar_swaps_template_indices_for_first_two_rows()
    {
        var a = new CookingInstructionPdfLineModel
        {
            OrderNo = "",
            ParentItemCode = "P0",
            ParentItemName = "親A",
            PlannedQuantityDisplay = "1",
            PlanUnitName = "u0",
            ChildItemCode = "C0",
            ChildItemName = "子A",
            ChildRequiredQtyDisplay = "1",
            ChildUnitName = "g",
            NeedDateDisplay = "2025/01/01",
            SlotDisplay = "便",
            WorkplaceNames = ""
        };
        var b = new CookingInstructionPdfLineModel
        {
            OrderNo = "",
            ParentItemCode = "P1",
            ParentItemName = "親B",
            PlannedQuantityDisplay = "2",
            PlanUnitName = "u1",
            ChildItemCode = "C1",
            ChildItemName = "子B",
            ChildRequiredQtyDisplay = "2",
            ChildUnitName = "g",
            NeedDateDisplay = "2025/01/01",
            SlotDisplay = "便",
            WorkplaceNames = ""
        };

        var tags = CookingInstructionPdfService.BuildPageTagValues(new[] { a, b }, "便", "2025/01/01");

        Assert.Equal("u0", tags["UNITPAR01"]);
        Assert.Equal("u1", tags["UNITPAR00"]);
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

    [Theory]
    [InlineData("O", "1", "O\n1")]
    [InlineData("", "1", "1")]
    [InlineData("O", "", "O")]
    [InlineData("  ", "  ", "")]
    public void BuildParentTopCell_joins_order_and_quantity(string order, string qty, string expected)
    {
        Assert.Equal(expected, CookingInstructionPdfService.BuildParentTopCell(order, qty));
    }
}

