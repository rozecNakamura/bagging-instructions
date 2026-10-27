using System.Text;
using BaggingInstructions.Api.Services;
using BaggingInstructions.Api.QueryResults;

namespace BaggingInstructions.Api.Tests;

public class PlanningPreparationWorkServiceTests
{
    private static PlanningPreparationCsvRow Row(string finalDate, string finalItemcode, string parentItemcode, string childItemcode, decimal? productionOrder) => new()
    {
        DeliveryDate = "2026/10/20",
        FinalManufacturingDate = finalDate,
        FinalItemcode = finalItemcode,
        ParentItemcode = parentItemcode,
        ChildItemcode = childItemcode,
        ProductionOrder = productionOrder
    };

    [Fact]
    public void SortCsvRows_orders_by_final_date_final_parent_then_recipe_order()
    {
        var rows = new[]
        {
            Row("2026/10/22", "F1", "P1", "C1", 1m),
            Row("2026/10/21", "F2", "P1", "C1", 1m),
            Row("2026/10/21", "F1", "P2", "C1", 1m),
            Row("2026/10/21", "F1", "P1", "C9", null),
            Row("2026/10/21", "F1", "P1", "C3", 2m),
            Row("2026/10/21", "F1", "P1", "C5", 1m)
        };

        var sorted = PlanningPreparationWorkService.SortCsvRows(rows);

        Assert.Equal(
            new[]
            {
                "2026/10/21/F1/P1/C5", "2026/10/21/F1/P1/C3", "2026/10/21/F1/P1/C9",
                "2026/10/21/F1/P2/C1", "2026/10/21/F2/P1/C1", "2026/10/22/F1/P1/C1"
            },
            sorted.Select(r => $"{r.FinalManufacturingDate}/{r.FinalItemcode}/{r.ParentItemcode}/{r.ChildItemcode}"));
    }

    [Fact]
    public void WriteCsvUtf8Bom_outputs_requested_columns_in_order()
    {
        var row = new PlanningPreparationCsvRow
        {
            OrderNo = "101・102",
            Slot = "1便",
            FinalManufacturingDate = "2026/10/21",
            DeliveryDate = "2026/10/20",
            FinalItemcode = "F1、F2",
            FinalItemname = "製品A、製品B",
            ParentItemcode = "P1",
            ParentItemname = "親",
            ChildItemcode = "C1",
            ChildItemname = "子,1",
            Quantity = "3000",
            Unit = "g",
            PurchaseQuantity = "3",
            PurchaseUnit = "kg"
        };

        var bytes = PlanningPreparationWorkService.WriteCsvUtf8Bom(new[] { row });
        var preamble = Encoding.UTF8.GetPreamble();
        Assert.Equal(preamble, bytes.Take(preamble.Length));

        var lines = Encoding.UTF8.GetString(bytes, preamble.Length, bytes.Length - preamble.Length)
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("注番,製造便,最終製品製造日,納期,最終製品コード,最終製品,親品目コード,親品目,子品目コード,子品目,数量,単位,購買単位数量,購買単位", lines[0]);
        Assert.Equal("101・102,1便,2026/10/21,2026/10/20,F1、F2,製品A、製品B,P1,親,C1,\"子,1\",3000,g,3,kg", lines[1]);
    }

    private static PlanningPreparationWorkBomAncestorSqlRow Node(string code, string name, int depth) =>
        new() { Itemcode = code, Itemname = name, Depth = depth };

    private static PlanningPreparationWorkCandidateOrderSqlRow Order(string code, int? day) =>
        new() { Itemcode = code, ProductNo = "PN", ManufacturingDate = day is int d ? new DateOnly(2026, 10, d) : null };

    [Fact]
    public void SelectFinalProduct_picks_deepest_ancestor_with_same_productno()
    {
        var ancestors = new[] { Node("P1", "親", 0), Node("M1", "中間", 1), Node("F2", "製品B", 2), Node("F1", "製品A", 2), Node("TOP", "最上位", 3) };
        // TOP は同じ productno のオーダーが無いので対象外。BOM 外の品目（X1）も対象外。
        var orders = new[] { Order("P1", 20), Order("M1", 20), Order("F2", 22), Order("F1", 21), Order("F1", 21), Order("X1", 25) };

        var final = PlanningPreparationWorkService.SelectFinalProduct(ancestors, orders);

        Assert.NotNull(final);
        Assert.Equal("F1、F2", final!.Itemcodes);
        Assert.Equal("製品A、製品B", final.Itemnames);
        Assert.Equal("2026/10/21、2026/10/22", final.ManufacturingDates);
    }

    [Fact]
    public void SelectFinalProduct_returns_null_when_no_ancestor_has_order()
    {
        var final = PlanningPreparationWorkService.SelectFinalProduct(
            new[] { Node("P1", "親", 0) }, new[] { Order("X1", 20) });

        Assert.Null(final);
    }

    [Fact]
    public void OrdersInWindow_keeps_earliest_order_per_item_from_delivery_date_to_five_days_after()
    {
        var orders = new[] { Order("A", 29), Order("B", 31), Order("B", 30), Order("C", 31), Order("D", null) }
            .Concat(new[]
            {
                new PlanningPreparationWorkCandidateOrderSqlRow { Itemcode = "E", ManufacturingDate = new DateOnly(2026, 11, 4) },
                new PlanningPreparationWorkCandidateOrderSqlRow { Itemcode = "F", ManufacturingDate = new DateOnly(2026, 11, 5) }
            });

        var inWindow = PlanningPreparationWorkService.OrdersInWindow(orders, new DateOnly(2026, 10, 30));

        Assert.Equal(new[] { "B", "C", "E" }, inWindow.Select(o => o.Itemcode));
        Assert.Equal(new DateOnly(2026, 10, 30), inWindow[0].ManufacturingDate);
    }

    [Fact]
    public void SelectFinalProduct_excludes_bom_ancestors_without_planning_orders()
    {
        // 親品目 P1 を使う最上位品目は F1～F3 だが、計画オーダーがあるのは F1 だけ
        var ancestors = new[] { Node("P1", "人参", 0), Node("F1", "製品A", 1), Node("F2", "製品B", 1), Node("F3", "製品C", 2) };
        var orders = new[] { Order("P1", 30), Order("F1", 31) };

        var final = PlanningPreparationWorkService.SelectFinalProduct(ancestors, orders);

        Assert.Equal("F1", final!.Itemcodes);
        Assert.Equal("製品A", final.Itemnames);
        Assert.Equal("2026/10/31", final.ManufacturingDates);
    }
}
