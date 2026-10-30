using BaggingInstructions.Api.Entities;
using BaggingInstructions.Api.Services;

namespace BaggingInstructions.Api.Tests;

public class SalesOrderStatusFilterTests
{
    private static SalesOrderLine Line(string? lineStatus, string? addinfo07, string? orderStatus = "draft")
    {
        var order = new SalesOrder { SalesOrderId = 1, Status = orderStatus };
        var line = new SalesOrderLine
        {
            SalesOrderLineId = 1,
            SalesOrder = order,
            Status = lineStatus,
            Addinfo = new SalesOrderLineAddinfo { SalesOrderLineId = 1, Addinfo07 = addinfo07 }
        };
        order.SalesOrderLines.Add(line);
        return line;
    }

    [Theory]
    [InlineData("confirmed", null, true)]
    [InlineData("confirmed", "order2", true)]
    [InlineData("draft", null, false)]
    [InlineData("draft", "order2", true)]
    [InlineData("draft", " order2 ", true)]
    [InlineData("draft", "ORDER2", true)]
    [InlineData("draft", "order1", false)]
    [InlineData("cancelled", "order2", false)]
    [InlineData("cancelled", null, false)]
    public void ConfirmedOrOrder2Line_includes_planned_order2_but_never_cancelled(
        string? status, string? addinfo07, bool expected)
    {
        var predicate = SalesOrderStatusFilter.ConfirmedOrOrder2Line.Compile();
        Assert.Equal(expected, predicate(Line(status, addinfo07)));
    }

    [Theory]
    [InlineData("confirmed", "draft", true)]
    [InlineData("confirmed", null, true)]
    [InlineData("draft", "confirmed", false)]
    public void ConfirmedOrOrder2Line_uses_line_status_not_order_status(
        string? lineStatus, string? orderStatus, bool expected)
    {
        var predicate = SalesOrderStatusFilter.ConfirmedOrOrder2Line.Compile();
        Assert.Equal(expected, predicate(Line(lineStatus, null, orderStatus)));
    }

    [Theory]
    [InlineData("confirmed", null)]
    [InlineData("draft", "order2")]
    public void ConfirmedOrOrder2Line_excludes_cancelled_order_header(string? lineStatus, string? addinfo07)
    {
        var predicate = SalesOrderStatusFilter.ConfirmedOrOrder2Line.Compile();
        Assert.False(predicate(Line(lineStatus, addinfo07, "cancelled")));
    }

    [Theory]
    [InlineData("confirmed", null, true)]
    [InlineData("draft", null, false)]
    [InlineData("draft", "order2", true)]
    [InlineData("cancelled", "order2", false)]
    public void ConfirmedOrOrder2Order_includes_planned_order2_but_never_cancelled(
        string? status, string? addinfo07, bool expected)
    {
        var predicate = SalesOrderStatusFilter.ConfirmedOrOrder2Order.Compile();
        Assert.Equal(expected, predicate(Line(status, addinfo07).SalesOrder));
    }

    [Fact]
    public void ConfirmedOrOrder2Order_matches_when_any_line_is_confirmed()
    {
        var order = new SalesOrder { SalesOrderId = 1, Status = "draft" };
        order.SalesOrderLines.Add(new SalesOrderLine { SalesOrderLineId = 1, Status = "draft", SalesOrder = order });
        order.SalesOrderLines.Add(new SalesOrderLine { SalesOrderLineId = 2, Status = "confirmed", SalesOrder = order });

        var predicate = SalesOrderStatusFilter.ConfirmedOrOrder2Order.Compile();
        Assert.True(predicate(order));
    }

    [Fact]
    public void ConfirmedOrOrder2Order_excludes_cancelled_order_header()
    {
        var predicate = SalesOrderStatusFilter.ConfirmedOrOrder2Order.Compile();
        Assert.False(predicate(Line("confirmed", null, "cancelled").SalesOrder));
    }

    /// <summary>指定ステータスの明細を並べた受注を作る。</summary>
    private static SalesOrder OrderWithLines(string? orderStatus, params string?[] lineStatuses)
    {
        var order = new SalesOrder { SalesOrderId = 1, Status = orderStatus };
        var id = 0;
        foreach (var status in lineStatuses)
        {
            id++;
            order.SalesOrderLines.Add(new SalesOrderLine
            {
                SalesOrderLineId = id,
                SalesOrder = order,
                Status = status,
                Addinfo = new SalesOrderLineAddinfo { SalesOrderLineId = id }
            });
        }
        return order;
    }

    [Fact]
    public void ConfirmedOrOrder2Line_includes_open_when_order_has_no_confirmed_line()
    {
        var order = OrderWithLines("confirmed", "open", "open");
        var predicate = SalesOrderStatusFilter.ConfirmedOrOrder2Line.Compile();
        Assert.All(order.SalesOrderLines, l => Assert.True(predicate(l)));
    }

    [Fact]
    public void ConfirmedOrOrder2Line_excludes_open_when_order_has_a_confirmed_line()
    {
        var order = OrderWithLines("confirmed", "confirmed", "open");
        var predicate = SalesOrderStatusFilter.ConfirmedOrOrder2Line.Compile();
        var lines = order.SalesOrderLines.ToList();
        Assert.True(predicate(lines[0]));
        Assert.False(predicate(lines[1]));
    }

    [Theory]
    [InlineData("draft")]
    [InlineData("cancelled")]
    public void ConfirmedOrOrder2Line_open_fallback_applies_only_to_open(string? lineStatus)
    {
        var order = OrderWithLines("confirmed", lineStatus);
        var predicate = SalesOrderStatusFilter.ConfirmedOrOrder2Line.Compile();
        Assert.False(predicate(order.SalesOrderLines.First()));
    }

    [Fact]
    public void ConfirmedOrOrder2Line_open_fallback_excludes_cancelled_order_header()
    {
        var order = OrderWithLines("cancelled", "open");
        var predicate = SalesOrderStatusFilter.ConfirmedOrOrder2Line.Compile();
        Assert.False(predicate(order.SalesOrderLines.First()));
    }

    [Fact]
    public void ConfirmedOrOrder2Order_matches_when_all_lines_are_open()
    {
        var predicate = SalesOrderStatusFilter.ConfirmedOrOrder2Order.Compile();
        Assert.True(predicate(OrderWithLines("confirmed", "open", "open")));
    }

    /// <summary>明細 1 件だけの受注（個人配送の取込形式）を作る。</summary>
    private static SalesOrderLine SingleLineOrder(
        long id,
        string? lineStatus,
        string? orderStatus = "confirmed",
        string location = "001980",
        string deliveryDate = "2026-10-29",
        string mealTime = "3",
        string foodType = "5AD",
        string? addinfo07 = "order1")
    {
        var order = new SalesOrder
        {
            SalesOrderId = id,
            Status = orderStatus,
            CustomerCode = "300",
            CustomerDeliveryLocationCode = location
        };
        var line = new SalesOrderLine
        {
            SalesOrderLineId = id,
            SalesOrderId = id,
            SalesOrder = order,
            Status = lineStatus,
            PlannedDeliveryDate = DateOnly.Parse(deliveryDate),
            Addinfo = new SalesOrderLineAddinfo
            {
                SalesOrderLineId = id,
                Addinfo02 = foodType,
                Addinfo05 = mealTime,
                Addinfo07 = addinfo07
            }
        };
        order.SalesOrderLines.Add(line);
        return line;
    }

    /// <summary>読み込み済み明細 <paramref name="loaded"/> に対し、DB 全体を <paramref name="allLines"/> として取消判定する。</summary>
    private static List<SalesOrderLine> ExcludeCancelledOpen(SalesOrderLine[] loaded, params SalesOrderLine[] allLines) =>
        SalesOrderStatusFilter.ExcludeCancelledOpenLines(allLines.AsQueryable(), loaded.ToList());

    [Fact]
    public void ExcludeCancelledOpenLines_excludes_open_when_same_meal_is_confirmed_in_other_order()
    {
        // 御飯のみ取消: 御飯は予定取込のまま open、おかずは別受注で確定済み
        var rice = SingleLineOrder(1, "open", orderStatus: "draft");
        var sideDish = SingleLineOrder(2, "confirmed");

        var result = ExcludeCancelledOpen([rice, sideDish], rice, sideDish);
        Assert.Equal([sideDish], result);
    }

    [Fact]
    public void ExcludeCancelledOpenLines_uses_confirmed_lines_not_in_loaded_set()
    {
        // ご飯盛り付け指示書のように御飯品目だけを読み込む場合も、DB 上のおかずの確定で判定する
        var rice = SingleLineOrder(1, "open", orderStatus: "draft");
        var sideDish = SingleLineOrder(2, "confirmed");

        Assert.Empty(ExcludeCancelledOpen([rice], rice, sideDish));
    }

    [Theory]
    [InlineData("001981", "2026-10-29", "3", "5AD")]
    [InlineData("001980", "2026-10-30", "3", "5AD")]
    [InlineData("001980", "2026-10-29", "2", "5AD")]
    [InlineData("001980", "2026-10-29", "3", "5AE")]
    public void ExcludeCancelledOpenLines_keeps_open_when_confirmed_line_is_for_another_meal(
        string location, string deliveryDate, string mealTime, string foodType)
    {
        var open = SingleLineOrder(1, "open", orderStatus: "draft");
        var other = SingleLineOrder(2, "confirmed", location: location, deliveryDate: deliveryDate,
            mealTime: mealTime, foodType: foodType);

        Assert.Equal([open], ExcludeCancelledOpen([open], open, other));
    }

    [Fact]
    public void ExcludeCancelledOpenLines_ignores_confirmed_line_of_cancelled_order()
    {
        var open = SingleLineOrder(1, "open", orderStatus: "draft");
        var cancelled = SingleLineOrder(2, "confirmed", orderStatus: "cancelled");

        Assert.Equal([open], ExcludeCancelledOpen([open], open, cancelled));
    }

    [Fact]
    public void ExcludeCancelledOpenLines_keeps_order2_even_when_same_meal_is_confirmed()
    {
        var order2 = SingleLineOrder(1, "open", orderStatus: "draft", addinfo07: " ORDER2 ");
        var confirmed = SingleLineOrder(2, "confirmed");

        Assert.Equal([order2], ExcludeCancelledOpen([order2], order2, confirmed));
    }

    [Fact]
    public void ExcludeCancelledOpenLines_matches_codes_ignoring_surrounding_spaces()
    {
        var open = SingleLineOrder(1, "open", orderStatus: "draft", mealTime: "3 ", foodType: " 5AD");
        var confirmed = SingleLineOrder(2, "confirmed");

        Assert.Empty(ExcludeCancelledOpen([open], open, confirmed));
    }

    [Theory]
    [InlineData("order2", true)]
    [InlineData(" order2 ", true)]
    [InlineData("Order2", true)]
    [InlineData("order", false)]
    [InlineData(null, false)]
    public void IsOrder2_trims_and_ignores_case(string? addinfo07, bool expected)
    {
        Assert.Equal(expected, SalesOrderStatusFilter.IsOrder2(addinfo07));
    }
}
