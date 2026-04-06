using BaggingInstructions.Api.Services;
using Xunit;

namespace BaggingInstructions.Api.Tests;

public class CabWinnaSotiProductionInstructionPdfServiceTests
{
    [Fact]
    public void BuildPageTagDictionary_HeaderAndFirstMaterialRow()
    {
        var line = ProductionInstructionPdfTestModels.ChildLine("701", "3便", "GP", "親品", "GC", "子品", "3", "個", "95", "規格Z");
        var tags = CabWinnaSotiProductionInstructionPdfService.BuildPageTagDictionary(line, new[] { line });

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
        Assert.Equal("", tags["BBD00"]);
    }

    [Fact]
    public void Paging_TwentyChildren_OneOrder_YieldsTwoPages()
    {
        var lines = Enumerable.Range(1, 20)
            .Select(i => ProductionInstructionPdfTestModels.ChildLine("1", "1便", "P", "親", $"C{i}", $"子{i}", $"{i}", "g", "90"))
            .ToList();

        var pages = ProductionInstructionRxzPaging.BuildMultiPageTagDictionaries(
            lines,
            CabWinnaSotiProductionInstructionPdfService.MaterialSlotsPerPage,
            CabWinnaSotiProductionInstructionPdfService.BuildPageTagDictionary);

        Assert.Equal(2, pages.Count);
        Assert.Equal("C1", pages[0]["MATCD00"]);
        Assert.Equal("C18", pages[0]["MATCD17"]);
        Assert.Equal("C19", pages[0]["MATCD18"]);
        Assert.Equal("C20", pages[1]["MATCD00"]);
        Assert.Equal("", pages[1]["MATCD01"]);
    }
}
