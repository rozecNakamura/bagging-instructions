using BaggingInstructions.Api.DTOs;
using BaggingInstructions.Api.Services;
using Xunit;

namespace BaggingInstructions.Api.Tests;

public class GanmonoTakiaiProductionInstructionPdfServiceTests
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
            NeedDateDisplay = "2025/03/01"
        };
    }

    [Fact]
    public void BuildPageTagDictionary_HeaderAndMainRow()
    {
        var line = ChildLine("701", "3便", "GP", "親品", "GC", "子品", "3", "個", "95", "規格Z");
        var tags = GanmonoTakiaiProductionInstructionPdfService.BuildPageTagDictionary(line, new[] { line });

        Assert.Equal("GP", tags["ITEMPARCD"]);
        Assert.Equal("親品", tags["ITEMPARNM"]);
        Assert.Equal("2025/03/01", tags["ITEMMAKEDATE"]);
        Assert.Equal("3便", tags["ITEMCLASS"]);
        Assert.Equal("GC", tags["MATCD00"]);
        Assert.Equal("子品", tags["MATNM00"]);
        Assert.Equal("95", tags["YIELD00"]);
        Assert.Equal("規格Z", tags["CUTITEMNM00"]);
        Assert.Equal("3", tags["FILLQUN00"]);
        Assert.Equal("3 個", tags["USEQUN00"]);
    }

    [Fact]
    public void BuildPageTagDictionary_EighthChild_GoesToSubRow()
    {
        var h = new ProductionInstructionPdfLineModel
        {
            OrderNo = "8",
            SlotDisplay = "B便",
            ParentItemCode = "P",
            ParentItemName = "親",
            PlannedQuantityDisplay = "1",
            PlanUnitName = "個",
            NeedDateDisplay = "2025/04/01"
        };
        var children = Enumerable.Range(1, 8).Select(i =>
            ChildLine("8", "B便", "P", "親", $"C{i}", $"子{i}", $"{i}", "g", "90")).ToList();

        var tags = GanmonoTakiaiProductionInstructionPdfService.BuildPageTagDictionary(h, children);

        Assert.Equal("C8", tags["SUBMATCD00"]);
        Assert.Equal("子8", tags["SUBMATNM00"]);
        Assert.Equal("90", tags["COMPRATE00"]);
        Assert.Equal("8", tags["SUBFILLQUN00"]);
        Assert.Equal("8 g", tags["SUBUSEQUN00"]);
    }

    [Fact]
    public void GroupByOrderSorted_MatchesSharedHelper()
    {
        var lines = new List<ProductionInstructionPdfLineModel>
        {
            ChildLine("30", "3便", "P", "親", "a", "A", "1", "g", "1"),
            ChildLine("10", "1便", "P", "親", "b", "B", "1", "g", "1"),
            ChildLine("10", "1便", "P", "親", "c", "C", "1", "g", "1")
        };

        var fromService = HoikoloProductionInstructionPdfService.GroupLinesForHoikolo(lines);
        var fromShared = ProductionInstructionPdfLineGrouping.GroupByOrderSortedBySlotThenOrderId(lines);
        Assert.Equal(fromService.Count, fromShared.Count);
        Assert.Equal(fromService[0][0].OrderNo, fromShared[0][0].OrderNo);
        Assert.Equal(fromService[1][0].OrderNo, fromShared[1][0].OrderNo);
    }
}
