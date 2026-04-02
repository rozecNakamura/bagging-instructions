using BaggingInstructions.Api.DTOs;
using BaggingInstructions.Api.Services;

namespace BaggingInstructions.Api.Tests;

public class AcceptanceRecordPdfServiceTests
{
    [Fact]
    public void BuildPageTagValues_maps_row_and_header_and_blanks_temperature()
    {
        var line = new AcceptanceRecordPdfLineModel
        {
            SalesOrderLineId = 1,
            EatDateDisplay = "2026/04/02",
            SlotDisplay = "昼便",
            ChildItemText = "ITEM01 テスト商品",
            MealCountDisplay = "12.5",
            TotalQtyDisplay = "100",
            UnitName = "食"
        };

        var tags = AcceptanceRecordPdfService.BuildPageTagValues(
            new[] { line },
            "店舗A",
            "2026/04/01",
            "2026/04/02");

        Assert.Equal("店舗A", tags["LOCATION"]);
        Assert.Equal("2026/04/01", tags["OUTDATE"]);
        Assert.Equal("2026/04/02", tags["DELVDATE"]);
        Assert.Equal("", tags["DELVTIME"]);
        Assert.Equal("", tags["CARTEMP"]);
        Assert.Equal("", tags["ITEMTEMP00"]);
        Assert.Equal("", tags["COLDTEMP00"]);

        Assert.Equal("2026/04/02", tags["EATDATE00"]);
        Assert.Equal("昼便", tags["EATTIME00"]);
        Assert.Equal("ITEM01 テスト商品", tags["ITEMNUM00"]);
        Assert.Equal("12.5", tags["COUNT00"]);
        Assert.Equal("100", tags["ALLQUN00"]);
        Assert.Equal("食", tags["QUANTITYUNIT00"]);
        Assert.Equal("", tags["COMMENT00"]);
        Assert.Equal("", tags["LIMIT00"]);
        Assert.Equal("", tags["AMOUNT00"]);
        Assert.Equal("", tags["ADD00"]);

        Assert.Equal("", tags["ITEMNM00"]);
        Assert.Equal("", tags["EATDATE01"]);
        Assert.Equal("", tags["ITEMNUM01"]);
    }
}
