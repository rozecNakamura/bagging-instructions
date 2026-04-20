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
            DeliveryLocationName = "テスト施設",
            PlannedShipDate = new DateOnly(2026, 4, 1),
            PlannedDeliveryDate = new DateOnly(2026, 4, 2),
            EatDateDisplay = "04/02",
            SlotDisplay = "昼便",
            ItemCode = "ITEM01",
            ItemName = "テスト商品",
            ChildItemText = "ITEM01 テスト商品",
            MealCountDisplay = "12.5",
            TotalQtyDisplay = "100",
            UnitName = "食"
        };

        var tags = AcceptanceRecordPdfService.BuildPageTagValues(
            new[] { line },
            "フォールバック店舗",
            "2099/01/01");

        Assert.Equal("テスト施設", tags["LOCATION"]);
        Assert.Equal("2026/04/01", tags["OUTDATE"]);
        Assert.Equal("【　　】", tags["DELVDATE"]);
        Assert.Equal("【　　】", tags["DELVTIME"]);
        Assert.Equal("", tags["CARTEMP"]);
        Assert.Equal("", tags["ITEMTEMP00"]);
        Assert.Equal("", tags["COLDTEMP00"]);

        Assert.Equal("04/02", tags["EATDATE00"]);
        Assert.Equal("昼便", tags["EATTIME00"]);
        Assert.Equal("ITEM01", tags["ITEMNUM00"]);
        Assert.Equal("12.5", tags["COUNT00"]);
        Assert.Equal("100", tags["ALLQUN00"]);
        Assert.Equal("食", tags["QUANTITYUNIT00"]);
        Assert.Equal("", tags["COMMENT00"]);
        Assert.Equal("", tags["LIMIT00"]);
        Assert.Equal("", tags["AMOUNT00"]);
        Assert.Equal("", tags["ADD00"]);

        Assert.Equal("テスト商品", tags["ITEMNM00"]);
        Assert.Equal("", tags["EATDATE01"]);
        Assert.Equal("", tags["ITEMNUM01"]);
    }

    [Fact]
    public void BuildPageTagValues_uses_fallback_when_line_has_no_header_dates()
    {
        var line = new AcceptanceRecordPdfLineModel
        {
            SalesOrderLineId = 1,
            DeliveryLocationName = "",
            PlannedShipDate = null,
            PlannedDeliveryDate = null,
            EatDateDisplay = "2026/04/02",
            SlotDisplay = "昼",
            ChildItemText = "X",
            MealCountDisplay = "1",
            TotalQtyDisplay = "1",
            UnitName = "個"
        };

        var tags = AcceptanceRecordPdfService.BuildPageTagValues(
            new[] { line },
            "店舗FB",
            "2026/03/01");

        Assert.Equal("店舗FB", tags["LOCATION"]);
        Assert.Equal("2026/03/01", tags["OUTDATE"]);
        Assert.Equal("【　　】", tags["DELVDATE"]);
        Assert.Equal("【　　】", tags["DELVTIME"]);
    }

    [Fact]
    public void SplitIntoPages_breaks_on_group_change_and_at_14_rows()
    {
        var d1 = new DateOnly(2026, 4, 10);
        var d2 = new DateOnly(2026, 4, 11);
        List<AcceptanceRecordPdfLineModel> Lines(string fac, DateOnly? ship, DateOnly? delv, int count, long idBase)
        {
            var list = new List<AcceptanceRecordPdfLineModel>();
            for (var i = 0; i < count; i++)
            {
                list.Add(new AcceptanceRecordPdfLineModel
                {
                    SalesOrderLineId = idBase + i,
                    DeliveryLocationName = fac,
                    PlannedShipDate = ship,
                    PlannedDeliveryDate = delv,
                    EatDateDisplay = "",
                    SlotDisplay = "",
                    ChildItemText = $"item{i}",
                    MealCountDisplay = "1",
                    TotalQtyDisplay = "1",
                    UnitName = ""
                });
            }

            return list;
        }

        var a = Lines("施設A", d1, d1, 15, 100);
        var b = Lines("施設B", d1, d1, 3, 200);
        var input = a.Concat(b).ToList();

        var pages = AcceptanceRecordPdfService.SplitIntoPages(input);

        Assert.Equal(3, pages.Count);
        Assert.Equal(14, pages[0].Count);
        Assert.Single(pages[1]);
        Assert.Equal("施設A", pages[1][0].DeliveryLocationName);
        Assert.Equal(3, pages[2].Count);
        Assert.All(pages[2], l => Assert.Equal("施設B", l.DeliveryLocationName));
    }

    [Fact]
    public void SplitIntoPages_keeps_rows_on_one_page_when_only_meal_time_differs()
    {
        var d = new DateOnly(2026, 4, 10);
        var input = new List<AcceptanceRecordPdfLineModel>
        {
            new()
            {
                SalesOrderLineId = 1,
                DeliveryLocationName = "施設A",
                PlannedShipDate = d,
                PlannedDeliveryDate = d,
                SlotDisplay = "朝",
                EatDateDisplay = "04/10",
                ItemCode = "C1",
                ItemName = "N1",
                ChildItemText = "C1 N1",
                MealCountDisplay = "1",
                TotalQtyDisplay = "1",
                UnitName = ""
            },
            new()
            {
                SalesOrderLineId = 2,
                DeliveryLocationName = "施設A",
                PlannedShipDate = d,
                PlannedDeliveryDate = d,
                SlotDisplay = "昼",
                EatDateDisplay = "04/10",
                ItemCode = "C2",
                ItemName = "N2",
                ChildItemText = "C2 N2",
                MealCountDisplay = "1",
                TotalQtyDisplay = "1",
                UnitName = ""
            }
        };

        var pages = AcceptanceRecordPdfService.SplitIntoPages(input);

        Assert.Single(pages);
        Assert.Equal(2, pages[0].Count);
    }
}
