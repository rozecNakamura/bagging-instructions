using BaggingInstructions.Api.Services;

namespace BaggingInstructions.Api.Tests;

public class PreparationWorkReportOrderTests
{
    private static PreparationPdfLineModel PdfLine(string childItemcode, decimal? productionOrder) => new()
    {
        DateDisplay = "2026/08/14",
        WorkplaceCode = "3F",
        ManufacturingRouteCode = "01",
        SlotDisplay = "昼製造",
        MiddleClassificationCode = "M01",
        OrderNo = "43969",
        ParentItemcode = "4530200556",
        ChildItemcode = childItemcode,
        ProductionOrder = productionOrder
    };

    private static PreparationCsvRow CsvRow(string childItemcode, decimal? productionOrder) => new()
    {
        DeliveryDate = "2026/08/14",
        WorkplaceName = "3F_調理室",
        OrderNo = "43969",
        ParentItemcode = "4530200556",
        ChildItemcode = childItemcode,
        ProductionOrder = productionOrder
    };

    [Fact]
    public void SortPdfLines_orders_children_by_recipe_order_not_itemcode()
    {
        // 子品目コードの昇順とレシピ並び順が逆になるデータ
        var lines = new[]
        {
            PdfLine("7015200548", 3m),
            PdfLine("7020200581", 2m),
            PdfLine("7033100047", 1m)
        };

        var sorted = PreparationWorkReportSort.SortPdfLines(lines);

        Assert.Equal(new[] { "7033100047", "7020200581", "7015200548" }, sorted.ConvertAll(l => l.ChildItemcode));
    }

    [Fact]
    public void SortPdfLines_nullProductionOrder_goesLast()
    {
        var lines = new[]
        {
            PdfLine("7000000001", null),
            PdfLine("7099999999", 1m)
        };

        var sorted = PreparationWorkReportSort.SortPdfLines(lines);

        Assert.Equal(new[] { "7099999999", "7000000001" }, sorted.ConvertAll(l => l.ChildItemcode));
    }

    [Fact]
    public void SortPdfLines_sameProductionOrder_fallsBackToItemcode()
    {
        var lines = new[]
        {
            PdfLine("7020200581", 1m),
            PdfLine("7015200548", 1m)
        };

        var sorted = PreparationWorkReportSort.SortPdfLines(lines);

        Assert.Equal(new[] { "7015200548", "7020200581" }, sorted.ConvertAll(l => l.ChildItemcode));
    }

    [Fact]
    public void SortCsvRows_orders_children_by_recipe_order_not_itemcode()
    {
        var rows = new[]
        {
            CsvRow("7015200548", 3m),
            CsvRow("7033100047", 1m),
            CsvRow("7020200581", null)
        };

        var sorted = PreparationWorkReportSort.SortCsvRows(rows);

        Assert.Equal(new[] { "7033100047", "7015200548", "7020200581" }, sorted.ConvertAll(r => r.ChildItemcode));
    }

    [Fact]
    public void BuildAggregationKey_doesNotMerge_productNoOrder_withInheritedSlotMrpOrder()
    {
        // 44653: 自オーダーの productno から 1便
        var seiban1 = new PreparationLineHeaderRow
        {
            Ordertableid = 44653,
            ParentItemcode = "4545150009",
            ManufacturingRouteCode = "1",
            ProductNo = "MATSUYAMA|20260818|1|1|3150100480"
        };
        // 44654: 自オーダーの productno から 2便
        var seiban2 = new PreparationLineHeaderRow
        {
            Ordertableid = 44654,
            ParentItemcode = "4545150009",
            ManufacturingRouteCode = "2",
            ProductNo = "MATSUYAMA|20260818|2|1|3150110480"
        };
        // 44655: productno なし（MRP品）。親から 1便 を継承しているが 44653 とは合算しない
        var mrp = new PreparationLineHeaderRow
        {
            Ordertableid = 44655,
            ParentItemcode = "4545150009",
            ManufacturingRouteCode = "1",
            ProductNo = null
        };

        var keys = new[] { seiban1, seiban2, mrp }
            .GroupBy(PreparationWorkService.BuildAggregationKey)
            .ToList();

        Assert.Equal(3, keys.Count);
        Assert.NotEqual(
            PreparationWorkService.BuildAggregationKey(seiban1),
            PreparationWorkService.BuildAggregationKey(mrp));
    }

    [Fact]
    public void BuildAggregationKey_merges_sameSlot_sameItem_sameSeibanKind()
    {
        var a = new PreparationLineHeaderRow
        {
            Ordertableid = 44653,
            ParentItemcode = "4545150009",
            ManufacturingRouteCode = "1",
            ProductNo = "MATSUYAMA|20260818|1|1|3150100480"
        };
        var b = new PreparationLineHeaderRow
        {
            Ordertableid = 44660,
            ParentItemcode = "4545150009",
            ManufacturingRouteCode = "1",
            ProductNo = "MATSUYAMA|20260818|1|1|3150100999"
        };

        // 同一便・同一品目・ともに製番品は従来どおり合算する
        Assert.Equal(
            PreparationWorkService.BuildAggregationKey(a),
            PreparationWorkService.BuildAggregationKey(b));
    }

    [Fact]
    public void BuildPageTagValues_manufacturingRoute_showsSlotName()
    {
        var line = PdfLine("7015200548", 1m);
        line.HasProductNo = true;   // 袋品でも製造便欄には便名を出す

        var tags = PreparationWorkPdfService.BuildPageTagValues(
            new[] { line },
            "炒め物",
            line.DateDisplay,
            "3F_調理室",
            "",
            line.SlotDisplay);

        Assert.Equal("昼製造", tags["ITEMTYPE01"]);   // 製造便：
        Assert.Equal("炒め物", tags["GENRE01"]);      // 分類名：
        Assert.Equal("3F_調理室", tags["LOCATIONFROM01"]);
        Assert.Equal("2026/08/14", tags["DATE01"]);
    }
}
