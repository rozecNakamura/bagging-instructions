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
