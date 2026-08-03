using BaggingInstructions.Api.Entities;
using BaggingInstructions.Api.Services;

namespace BaggingInstructions.Api.Tests;

public class SalesOrderStatusFilterTests
{
    private static SalesOrderLine Line(string? status, string? addinfo07)
    {
        var order = new SalesOrder { SalesOrderId = 1, Status = status };
        var line = new SalesOrderLine
        {
            SalesOrderLineId = 1,
            SalesOrder = order,
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
