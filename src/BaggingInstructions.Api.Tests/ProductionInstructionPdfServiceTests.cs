using System.Globalization;
using BaggingInstructions.Api.DTOs;
using BaggingInstructions.Api.Services;
using Xunit;

namespace BaggingInstructions.Api.Tests;

public class ProductionInstructionPdfServiceTests
{
    [Fact]
    public void BuildPageTagValues_FillsHeaderAndRows()
    {
        var lines = new List<ProductionInstructionPdfLineModel>
        {
            new()
            {
                OrderNo = "1001",
                ParentItemCode = "P001",
                ParentItemName = "親品目１",
                PlannedQuantityDisplay = "10",
                PlanUnitName = "kg",
                ChildItemCode = "C001",
                ChildItemName = "子品目１",
                ChildSpec = "規格A",
                ChildRequiredQtyDisplay = "5",
                ChildUnitName = "g",
                NeedDateDisplay = "2024/04/01",
                SlotDisplay = "1便"
            }
        };

        var tags = ProductionInstructionPdfService.BuildPageTagValues(lines, "1便", "2024/04/01");

        Assert.Equal("2024/04/01", tags["DATE01"]);
        Assert.Equal("1便", tags["SLOT01"]);

        Assert.Equal("P001 親品目１", tags["PARENTNM00"]);
        Assert.Equal("C001 子品目１", tags["CHILDNM00"]);
        Assert.Equal("10", tags["PARENTQTY00"]);
        Assert.Equal("5", tags["CHILDQTY00"]);
        Assert.Equal("kg", tags["PARENTUNIT00"]);
        Assert.Equal("g", tags["CHILDUNIT00"]);
        Assert.Equal("1001", tags["ORDNO00"]);
        Assert.Equal("規格A", tags["CHILDSPEC00"]);
    }

    [Fact]
    public void BuildPageTagValues_ShortensLongUnits()
    {
        var lines = new List<ProductionInstructionPdfLineModel>
        {
            new()
            {
                OrderNo = "1002",
                ParentItemCode = "P002",
                ParentItemName = "親品目２",
                PlannedQuantityDisplay = "20",
                PlanUnitName = "longunit",
                ChildItemCode = "C002",
                ChildItemName = "子品目２",
                ChildSpec = "",
                ChildRequiredQtyDisplay = "8",
                ChildUnitName = "verylong",
                NeedDateDisplay = "2024/04/02",
                SlotDisplay = "2便"
            }
        };

        var tags = ProductionInstructionPdfService.BuildPageTagValues(lines, "2便", "2024/04/02");

        Assert.Equal("long", tags["PARENTUNIT00"]);
        Assert.Equal("very", tags["CHILDUNIT00"]);
    }
}

