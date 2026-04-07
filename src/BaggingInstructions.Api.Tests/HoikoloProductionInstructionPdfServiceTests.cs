using BaggingInstructions.Api.DTOs;
using BaggingInstructions.Api.Services;
using Xunit;

namespace BaggingInstructions.Api.Tests;

public class HoikoloProductionInstructionPdfServiceTests
{
    [Fact]
    public void BuildPageTagDictionary_LowerSection_Hoikolo_SplitsXrayAndClearsLot()
    {
        var header = ProductionInstructionPdfTestModels.ChildLine(
            "501", "1便", "P1", "親", "C1", "子", "1", "kg", "100", needDateDisplay: "2025/01/15");
        header.ParentItemPrintAddinfo = new ProductionInstructionParentItemAddinfoForPdf
        {
            Addinfo13 = "XrayA",
            Addinfo14 = "XrayB"
        };
        var tags = HoikoloProductionInstructionPdfService.BuildPageTagDictionary(header, new[] { header });
        Assert.Equal("XrayA", tags["XRAYSET_1"]);
        Assert.Equal("XrayB", tags["XRAYSET_5"]);
        Assert.Equal("", tags["LOTNO_1"]);
        Assert.Equal("", tags["LOTNO_5"]);
    }

    [Fact]
    public void BuildPageTagDictionary_HeaderAndMainRow()
    {
        var header = ProductionInstructionPdfTestModels.ChildLine(
            "501", "1便", "P1", "親", "C1", "子", "2.5", "kg", "100", "規格X", needDateDisplay: "2025/01/15");
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
        Assert.Equal("kg", tags["FILLQUNUNIT00"]);
        Assert.Equal("2.5", tags["USEQUN00"]);
        Assert.Equal("kg", tags["USEQUNUNIT00"]);
        Assert.Equal("2.5", tags["FILLQUNSUM"]);
        Assert.Equal("kg", tags["FILLQUNSUMUNIT"]);
        Assert.Equal("2.5", tags["USEQUNSUM"]);
        Assert.Equal("", tags["SUBFILLQUNSUM"]);
        Assert.Equal("", tags["SUBUSEQUN11"]);
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
        var children = Enumerable.Range(1, 6).Select(i =>
            ProductionInstructionPdfTestModels.ChildLine("9", "A便", "P", "親", $"C{i}", $"子{i}", $"{i}", "g", "99")).ToList();

        var tags = HoikoloProductionInstructionPdfService.BuildPageTagDictionary(h, children);

        Assert.Equal("C6", tags["SUBMATCD00"]);
        Assert.Equal("子6", tags["SUBMATNM00"]);
        Assert.Equal("99", tags["COMPRATE00"]);
        Assert.Equal("6", tags["SUBFILLQUN00"]);
        Assert.Equal("g", tags["SUBFILLQUNUNIT00"]);
        Assert.Equal("6", tags["SUBUSEQUN00"]);
        Assert.Equal("g", tags["SUBUSEQUNUNIT00"]);
        Assert.Equal("15", tags["FILLQUNSUM"]);
        Assert.Equal("g", tags["FILLQUNSUMUNIT"]);
        Assert.Equal("15", tags["USEQUNSUM"]);
        Assert.Equal("6", tags["SUBFILLQUNSUM"]);
        Assert.Equal("g", tags["SUBFILLQUNSUMUNIT"]);
        Assert.Equal("6", tags["SUBUSEQUN11"]);
    }

    [Fact]
    public void GroupLinesForHoikolo_OrdersBySlotThenOrderNo()
    {
        var lines = new List<ProductionInstructionPdfLineModel>
        {
            ProductionInstructionPdfTestModels.ChildLine("20", "2便", "P", "親", "a", "A", "1", "g", "1"),
            ProductionInstructionPdfTestModels.ChildLine("10", "1便", "P", "親", "b", "B", "1", "g", "1"),
            ProductionInstructionPdfTestModels.ChildLine("10", "1便", "P", "親", "c", "C", "1", "g", "1")
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
