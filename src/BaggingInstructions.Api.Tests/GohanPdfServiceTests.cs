using BaggingInstructions.Api.Services;

namespace BaggingInstructions.Api.Tests;

public class GohanPdfServiceTests
{
    [Fact]
    public void BuildTagValues_box_prints_quantity_as_gram_and_product_as_pack()
    {
        var rows = new[]
        {
            new GohanPrintRowDto
            {
                Delvedt = "2026-05-26",
                Addinfo05 = "2",
                Jobordmernm = "ごはん",
                Shpctrnm = "テスト納入場所A",
                Quantity = 30m,
                Addinfo01 = "150",
                Addinfo08 = "0BOX"
            }
        };

        var tags = GohanPdfService.BuildTagValues(rows);

        Assert.Equal("BOX", tags["TYPE"]);
        Assert.Equal("30", tags["GRAM00"]);
        Assert.Equal("4500", tags["PACK00"]);
        Assert.Equal("食", tags["UNIT00"]);
    }

    [Fact]
    public void BuildTagValues_individual_prints_addinfo01_as_gram_and_quantity_as_pack()
    {
        var rows = new[]
        {
            new GohanPrintRowDto
            {
                Delvedt = "2026-05-26",
                Addinfo05 = "2",
                Jobordmernm = "ごはん",
                Shpctrnm = "テスト納入場所A",
                Quantity = 12m,
                Addinfo01 = "150",
                Addinfo08 = "1個人"
            }
        };

        var tags = GohanPdfService.BuildTagValues(rows);

        Assert.Equal("個人", tags["TYPE"]);
        Assert.Equal("150", tags["GRAM00"]);
        Assert.Equal("12", tags["PACK00"]);
        Assert.Equal("個", tags["UNIT00"]);
    }

    [Fact]
    public void PreparePrintRows_box_aggregates_by_addinfo01()
    {
        var rows = new[]
        {
            new GohanPrintRowDto { Quantity = 10m, Addinfo01 = "150", Addinfo08 = "0BOX", Shpctrnm = "施設A" },
            new GohanPrintRowDto { Quantity = 20m, Addinfo01 = "150", Addinfo08 = "0BOX", Shpctrnm = "施設B" },
            new GohanPrintRowDto { Quantity = 5m, Addinfo01 = "180", Addinfo08 = "0BOX", Shpctrnm = "施設C" }
        };

        var aggregated = GohanPdfService.PreparePrintRows(rows);

        Assert.Equal(2, aggregated.Count);
        Assert.Equal(30m, aggregated[0].Quantity);
        Assert.Equal("150", aggregated[0].Addinfo01);
        Assert.Equal("施設A、施設B", aggregated[0].Shpctrnm);
    }

    [Fact]
    public void PreparePrintRows_individual_240_keeps_location_per_item_and_portion()
    {
        var rows = new[]
        {
            new GohanPrintRowDto
            {
                Cuscd = "240", Itemcd = "30110001", Shpctrcd = "L1", Shpctrnm = "施設A",
                Quantity = 10m, Addinfo01 = "150", Addinfo08 = "1個人"
            },
            new GohanPrintRowDto
            {
                Cuscd = "240", Itemcd = "30110001", Shpctrcd = "L2", Shpctrnm = "施設B",
                Quantity = 20m, Addinfo01 = "150", Addinfo08 = "1個人"
            },
            new GohanPrintRowDto
            {
                Cuscd = "240", Itemcd = "30110001", Shpctrcd = "L1", Shpctrnm = "施設A",
                Quantity = 5m, Addinfo01 = "180", Addinfo08 = "1個人"
            }
        };

        var aggregated = GohanPdfService.PreparePrintRows(rows);

        Assert.Equal(3, aggregated.Count);
        Assert.Contains(aggregated, r => r.Shpctrnm == "施設A" && r.Addinfo01 == "150" && r.Quantity == 10m);
        Assert.Contains(aggregated, r => r.Shpctrnm == "施設B" && r.Addinfo01 == "150" && r.Quantity == 20m);
        Assert.Contains(aggregated, r => r.Shpctrnm == "施設A" && r.Addinfo01 == "180" && r.Quantity == 5m);
    }

    [Fact]
    public void PreparePrintRows_individual_300_sums_quantity_by_item_and_portion()
    {
        var rows = new[]
        {
            new GohanPrintRowDto
            {
                Cuscd = "300", Itemcd = "30110001", Shpctrcd = "L1", Shpctrnm = "施設A",
                Quantity = 10m, Addinfo01 = "150", Addinfo08 = "1個人"
            },
            new GohanPrintRowDto
            {
                Cuscd = "300", Itemcd = "30110001", Shpctrcd = "L2", Shpctrnm = "施設B",
                Quantity = 20m, Addinfo01 = "150", Addinfo08 = "1個人"
            }
        };

        var aggregated = GohanPdfService.PreparePrintRows(rows);

        Assert.Single(aggregated);
        Assert.Equal(30m, aggregated[0].Quantity);
        Assert.Equal("30110001", aggregated[0].Itemcd);
        Assert.Equal("150", aggregated[0].Addinfo01);
        Assert.Equal(GohanPdfService.HomeIndividualLocationLabel, aggregated[0].Shpctrnm);
    }

    [Theory]
    [InlineData("0BOX", "BOX")]
    [InlineData("1個人", "個人")]
    [InlineData("", "")]
    public void ResolveType_maps_addinfo08(string addinfo08, string expected) =>
        Assert.Equal(expected, GohanPdfService.ResolveType(addinfo08));
}
