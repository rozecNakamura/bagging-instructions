using BaggingInstructions.Api.DTOs;
using BaggingInstructions.Api.Services;
using Xunit;

namespace BaggingInstructions.Api.Tests;

public class HoikoloProductionInstructionPdfServiceTests
{
    private static ProductionInstructionPdfLineModel ChildLine(
        string orderNo,
        string slot,
        string parentCode,
        string parentName,
        string childCode,
        string childName,
        string qty,
        string unit,
        string yield,
        string spec = "")
    {
        return new ProductionInstructionPdfLineModel
        {
            OrderNo = orderNo,
            SlotDisplay = slot,
            ParentItemCode = parentCode,
            ParentItemName = parentName,
            PlannedQuantityDisplay = "1",
            PlanUnitName = "kg",
            ChildItemCode = childCode,
            ChildItemName = childName,
            ChildSpec = spec,
            ChildRequiredQtyDisplay = qty,
            ChildUnitName = unit,
            ChildYieldPercentDisplay = yield,
            NeedDateDisplay = "2025/01/15"
        };
    }

    [Fact]
    public void BuildPageTagDictionary_HeaderAndMainRow()
    {
        var header = ChildLine("501", "1便", "P1", "親", "C1", "子", "2.5", "kg", "100", "規格X");
        var tags = HoikoloProductionInstructionPdfService.BuildPageTagDictionary(header, new[] { header });

        Assert.Equal("P1", tags["ITEMPARCD"]);
        Assert.Equal("親", tags["ITEMPARNM"]);
        Assert.Equal("2025/01/15", tags["ITEMMAKEDATE"]);
        Assert.Equal("1便", tags["ITEMCLASS"]);
        Assert.Equal("C1", tags["MATCD00"]);
        Assert.Equal("子", tags["MATNM00"]);
        Assert.Equal("100", tags["YIELD00"]);
        Assert.Equal("規格X", tags["CUTITEMNM00"]);
        Assert.Equal("2.5", tags["FILLQUN00"]);
        Assert.Equal("2.5 kg", tags["USEQUN00"]);
    }

    [Fact]
    public void BuildPageTagDictionary_SixthChild_GoesToSubRow()
    {
        var h = new ProductionInstructionPdfLineModel
        {
            OrderNo = "9",
            SlotDisplay = "A便",
            ParentItemCode = "P",
            ParentItemName = "親",
            PlannedQuantityDisplay = "1",
            PlanUnitName = "個",
            NeedDateDisplay = "2025/02/01"
        };
        var children = Enumerable.Range(1, 6).Select(i => ChildLine("9", "A便", "P", "親", $"C{i}", $"子{i}", $"{i}", "g", "99")).ToList();

        var tags = HoikoloProductionInstructionPdfService.BuildPageTagDictionary(h, children);

        Assert.Equal("C6", tags["SUBMATCD00"]);
        Assert.Equal("子6", tags["SUBMATNM00"]);
        Assert.Equal("99", tags["COMPRATE00"]);
        Assert.Equal("6", tags["SUBFILLQUN00"]);
        Assert.Equal("6 g", tags["SUBUSEQUN00"]);
    }

    [Fact]
    public void GroupLinesForHoikolo_OrdersBySlotThenOrderNo()
    {
        var lines = new List<ProductionInstructionPdfLineModel>
        {
            ChildLine("20", "2便", "P", "親", "a", "A", "1", "g", "1"),
            ChildLine("10", "1便", "P", "親", "b", "B", "1", "g", "1"),
            ChildLine("10", "1便", "P", "親", "c", "C", "1", "g", "1")
        };

        var groups = HoikoloProductionInstructionPdfService.GroupLinesForHoikolo(lines);
        Assert.Equal(2, groups.Count);
        Assert.Equal("10", groups[0][0].OrderNo);
        Assert.Equal(2, groups[0].Count);
        Assert.Equal("20", groups[1][0].OrderNo);
    }

    [Fact]
    public void ParseOrderKey_ParsesNumericOrderNo()
    {
        Assert.Equal(42L, HoikoloProductionInstructionPdfService.ParseOrderKey("42"));
        Assert.Equal(0L, HoikoloProductionInstructionPdfService.ParseOrderKey("x"));
    }
}
