using System.Text;
using BaggingInstructions.Api.Services;

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
        Assert.Equal("注番,最終製品製造日,納期,最終製品コード,最終製品,親品目コード,親品目,子品目コード,子品目,数量,単位,購買単位数量,購買単位", lines[0]);
        Assert.Equal("101・102,2026/10/21,2026/10/20,F1、F2,製品A、製品B,P1,親,C1,\"子,1\",3000,g,3,kg", lines[1]);
    }
}
