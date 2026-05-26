using BaggingInstructions.Api.Services;

namespace BaggingInstructions.Api.Tests;

public class BentoPdfServiceTests
{
    [Fact]
    public void PreparePrintRows_okazu_groups_by_date_time_location_food_type_info17_and_sums_quantity()
    {
        var rows = new[]
        {
            new BentoPrintRowDto
            {
                Delvedt = "2026-05-26",
                Addinfo05 = "2",
                Shpctrcd = "001",
                Shpctrnm = "施設A",
                Quantity = 10m,
                Info17 = "おかずA",
                FoodTypeName = "普通食"
            },
            new BentoPrintRowDto
            {
                Delvedt = "2026-05-26",
                Addinfo05 = "2",
                Shpctrcd = "001",
                Shpctrnm = "施設A",
                Quantity = 15m,
                Info17 = "おかずA",
                FoodTypeName = "普通食"
            },
            new BentoPrintRowDto
            {
                Delvedt = "2026-05-26",
                Addinfo05 = "2",
                Shpctrcd = "002",
                Shpctrnm = "施設B",
                Quantity = 20m,
                Info17 = "おかずB",
                FoodTypeName = "軟食"
            }
        };

        var aggregated = BentoPdfService.PreparePrintRowsOkazu(rows);

        Assert.Equal(2, aggregated.Count);
        Assert.Equal(25m, aggregated.First(r => r.Info17 == "おかずA").Quantity);
        Assert.Equal(20m, aggregated.First(r => r.Info17 == "おかずB").Quantity);
    }

    [Fact]
    public void PreparePrintRows_okazu_keeps_rows_separate_by_food_type()
    {
        var rows = new[]
        {
            new BentoPrintRowDto
            {
                Delvedt = "2026-05-26",
                Addinfo05 = "2",
                Shpctrcd = "001",
                Quantity = 10m,
                Info17 = "おかずA",
                FoodTypeName = "普通食"
            },
            new BentoPrintRowDto
            {
                Delvedt = "2026-05-26",
                Addinfo05 = "2",
                Shpctrcd = "001",
                Quantity = 10m,
                Info17 = "おかずA",
                FoodTypeName = "軟食"
            }
        };

        var aggregated = BentoPdfService.PreparePrintRowsOkazu(rows);

        Assert.Equal(2, aggregated.Count);
    }

    [Fact]
    public void PreparePrintRows_gohan_sums_quantity_by_item_and_portion()
    {
        var rows = new[]
        {
            new BentoPrintRowDto
            {
                Delvedt = "2026-05-26",
                Addinfo05 = "2",
                Itemcd = "30110001",
                Jobordmernm = "ごはん",
                Addinfo01 = "150",
                Quantity = 10m
            },
            new BentoPrintRowDto
            {
                Delvedt = "2026-05-26",
                Addinfo05 = "2",
                Itemcd = "30110001",
                Jobordmernm = "ごはん",
                Addinfo01 = "150",
                Quantity = 5m
            },
            new BentoPrintRowDto
            {
                Delvedt = "2026-05-26",
                Addinfo05 = "2",
                Itemcd = "30110001",
                Jobordmernm = "ごはん",
                Addinfo01 = "180",
                Quantity = 3m
            }
        };

        var aggregated = BentoPdfService.PreparePrintRowsGohan(rows);

        Assert.Equal(2, aggregated.Count);
        Assert.Equal(15m, aggregated.First(r => r.Addinfo01 == "150").Quantity);
        Assert.Equal(3m, aggregated.First(r => r.Addinfo01 == "180").Quantity);
    }

    [Fact]
    public void BuildTagValues_gohan_prints_type_portion_as_gram_and_quantity_as_pack()
    {
        var rows = new[]
        {
            new BentoPrintRowDto
            {
                Delvedt = "2026-05-26",
                Addinfo05 = "2",
                Jobordmernm = "ごはん",
                Addinfo01 = "150",
                Quantity = 12m
            }
        };

        var tags = BentoPdfService.BuildTagValues(rows, BentoSearchFilter.TypeGohan);

        Assert.Equal("ご飯", tags["TYPE"]);
        Assert.Equal("昼", tags["Time"]);
        Assert.Equal("ごはん", tags["ITEMNM00"]);
        Assert.Equal("150", tags["GRAM00"]);
        Assert.Equal("12", tags["PACK00"]);
    }

    [Fact]
    public void BuildTagValues_okazu_prints_type_food_type_name_and_info17()
    {
        var rows = new[]
        {
            new BentoPrintRowDto
            {
                Delvedt = "2026-05-26",
                Addinfo05 = "1",
                FoodTypeName = "普通食",
                Info17 = "おかずA",
                Quantity = 30m
            }
        };

        var tags = BentoPdfService.BuildTagValues(rows, BentoSearchFilter.TypeOkazu);

        Assert.Equal("おかず", tags["TYPE"]);
        Assert.Equal("朝", tags["Time"]);
        Assert.Equal("普通食", tags["ITEMNM00"]);
        Assert.Equal("おかずA", tags["LOCATIONNM00"]);
        Assert.Equal("30", tags["PACK00"]);
        Assert.Equal("", tags["GRAM00"]);
    }
}
