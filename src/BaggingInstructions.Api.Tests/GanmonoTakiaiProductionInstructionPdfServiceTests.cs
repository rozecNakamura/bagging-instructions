using BaggingInstructions.Api.DTOs;
using BaggingInstructions.Api.Services;
using Xunit;

namespace BaggingInstructions.Api.Tests;

public class GanmonoTakiaiProductionInstructionPdfServiceTests
{
    [Fact]
    public void BuildPageTagDictionary_LowerSection_FromParentItemPrintAddinfo()
    {
        var line = ProductionInstructionPdfTestModels.ChildLine("701", "3便", "GP", "親品", "GC", "子品", "3", "個", "95", "規格Z");
        line.ParentItemPrintAddinfo = new ProductionInstructionParentItemAddinfoForPdf
        {
            Addinfo05 = "BagName",
            Addinfo06 = "100x200",
            Addinfo07 = "Top",
            Addinfo08 = "VacA",
            Addinfo09 = "Set9",
            Addinfo10 = "StopPt",
            Addinfo11 = "Seal11",
            Addinfo12 = "Sp12",
            Addinfo13 = "Xa",
            Addinfo14 = "Xb",
            Addinfo15 = "HeatDev",
            Addinfo16 = "Store16",
            Addinfo17 = "Note17",
            SterItemPrange = 85.5m,
            SteriTimeSeconds = 90m
        };
        var tags = GanmonoTakiaiProductionInstructionPdfService.BuildPageTagDictionary(line, new[] { line });

        Assert.Equal("BagName", tags["PACKNAME_1"]);
        Assert.Equal("100x200", tags["PACKSIZE_1"]);
        Assert.Equal("Top", tags["PACKPRINT"]);
        Assert.Equal("VacA", tags["VACPACK"]);
        Assert.Equal("Set9", tags["VACSETNO"]);
        Assert.Equal("StopPt", tags["VACSTOPPOINT"]);
        Assert.Equal("Seal11", tags["SEALSETVAL"]);
        Assert.Equal("Sp12", tags["SPEED"]);
        Assert.Equal("Xa / Xb", tags["XRAYSET_1"]);
        Assert.Equal("HeatDev", tags["PACKLOCATION"]);
        Assert.Equal("Store16", tags["PACKBBD"]);
        Assert.Equal("Note17", tags["MANAGERNAME"]);
        Assert.Equal("85.5", tags["HEATTEMP"]);
        Assert.Equal("1.5", tags["HEATTIME"]);
        Assert.Equal("", tags["LOTNO"]);
    }

    [Fact]
    public void BuildPageTagDictionary_HeaderAndMainRow()
    {
        var line = ProductionInstructionPdfTestModels.ChildLine("701", "3便", "GP", "親品", "GC", "子品", "3", "個", "95", "規格Z");
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
        Assert.Equal("個", tags["FILLQUNUNIT00"]);
        Assert.Equal("3", tags["USEQUN00"]);
        Assert.Equal("個", tags["USEQUNUNIT00"]);
        Assert.Equal("3", tags["FILLQUNSUM"]);
        Assert.Equal("個", tags["FILLQUNSUMUNIT"]);
        Assert.Equal("3", tags["USEQUNSUM"]);
        Assert.Equal("", tags["SUBFILLQUNSUM"]);
        Assert.Equal("", tags["SUBUSEQUNSUM"]);
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
            ProductionInstructionPdfTestModels.ChildLine("8", "B便", "P", "親", $"C{i}", $"子{i}", $"{i}", "g", "90")).ToList();

        var tags = GanmonoTakiaiProductionInstructionPdfService.BuildPageTagDictionary(h, children);

        Assert.Equal("C8", tags["SUBMATCD00"]);
        Assert.Equal("子8", tags["SUBMATNM00"]);
        Assert.Equal("90", tags["COMPRATE00"]);
        Assert.Equal("8", tags["SUBFILLQUN00"]);
        Assert.Equal("g", tags["SUBFILLQUNUNIT00"]);
        Assert.Equal("8", tags["SUBUSEQUN00"]);
        Assert.Equal("g", tags["SUBUSEQUNUNIT00"]);
        Assert.Equal("28", tags["FILLQUNSUM"]);
        Assert.Equal("g", tags["FILLQUNSUMUNIT"]);
        Assert.Equal("28", tags["USEQUNSUM"]);
        Assert.Equal("8", tags["SUBFILLQUNSUM"]);
        Assert.Equal("g", tags["SUBFILLQUNSUMUNIT"]);
        Assert.Equal("8", tags["SUBUSEQUNSUM"]);
    }

    [Fact]
    public void GroupByOrderSorted_MatchesSharedHelper()
    {
        var lines = new List<ProductionInstructionPdfLineModel>
        {
            ProductionInstructionPdfTestModels.ChildLine("30", "3便", "P", "親", "a", "A", "1", "g", "1"),
            ProductionInstructionPdfTestModels.ChildLine("10", "1便", "P", "親", "b", "B", "1", "g", "1"),
            ProductionInstructionPdfTestModels.ChildLine("10", "1便", "P", "親", "c", "C", "1", "g", "1")
        };

        var fromService = HoikoloProductionInstructionPdfService.GroupLinesForHoikolo(lines);
        var fromShared = ProductionInstructionPdfLineGrouping.GroupByOrderSortedBySlotThenOrderId(lines);
        Assert.Equal(fromService.Count, fromShared.Count);
        Assert.Equal(fromService[0][0].OrderNo, fromShared[0][0].OrderNo);
        Assert.Equal(fromService[1][0].OrderNo, fromShared[1][0].OrderNo);
    }
}
