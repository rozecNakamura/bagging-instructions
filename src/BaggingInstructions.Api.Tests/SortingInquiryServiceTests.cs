using BaggingInstructions.Api.Core;
using BaggingInstructions.Api.Entities;
using BaggingInstructions.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace BaggingInstructions.Api.Tests;

public class SortingInquiryServiceTests
{
    private static AppDbContext CreateAppDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task SearchAsync_InvalidDate_ThrowsArgumentException()
    {
        await using var app = CreateAppDb();
        var svc = new SortingInquiryService(app);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.SearchAsync("2024", null, CancellationToken.None));
    }

    [Fact]
    public async Task SearchAsync_Filters_by_slot_and_aggregates_by_item_foodtype_and_store()
    {
        await using var app = CreateAppDb();

        var d = new DateOnly(2025, 7, 10);
        app.Customers.Add(new Customer { CustomerId = 1, CustomerCode = "CUST1", CustomerName = "得意先1" });
        app.CustomerDeliveryLocations.Add(new CustomerDeliveryLocation
        {
            DeliveryLocationId = 1,
            CustomerCode = "CUST1",
            LocationCode = "LOC1",
            LocationName = "店舗A"
        });
        app.SalesOrders.Add(new SalesOrder
        {
            SalesOrderId = 1,
            CustomerCode = "CUST1",
            CustomerDeliveryLocationCode = "LOC1"
        });
        app.Items.Add(new Item
        {
            ItemId = 1,
            ItemCd = "ITEM1",
            ItemName = "商品1",
            ActiveFlag = true
        });
        app.SalesOrderLines.Add(new SalesOrderLine
        {
            SalesOrderLineId = 1,
            SalesOrderId = 1,
            LineNo = 1,
            ItemCd = "ITEM1",
            Quantity = 4,
            PlannedDeliveryDate = d,
            SlotCode = "S1"
        });
        app.SalesOrderLines.Add(new SalesOrderLine
        {
            SalesOrderLineId = 2,
            SalesOrderId = 1,
            LineNo = 2,
            ItemCd = "ITEM1",
            Quantity = 2,
            PlannedDeliveryDate = d,
            SlotCode = "S1"
        });
        app.SalesOrderLineAddinfos.Add(new SalesOrderLineAddinfo
        {
            SalesOrderLineAddinfoId = 1,
            SalesOrderLineId = 1,
            Addinfo02 = "FT1",
            Addinfo02Name = "昼食"
        });
        app.SalesOrderLineAddinfos.Add(new SalesOrderLineAddinfo
        {
            SalesOrderLineAddinfoId = 2,
            SalesOrderLineId = 2,
            Addinfo02 = "FT1",
            Addinfo02Name = "昼食"
        });
        await app.SaveChangesAsync();

        var svc = new SortingInquiryService(app);

        var allSlots = await svc.SearchAsync("20250710", Array.Empty<string>());
        Assert.Single(allSlots.Rows);
        Assert.Equal(6, allSlots.Rows[0].QuantitiesByStore["CUST1|LOC1"]);
        Assert.Equal("昼食", allSlots.Rows[0].FoodType);

        var filtered = await svc.SearchAsync("20250710", new[] { "OTHER" });
        Assert.Empty(filtered.Rows);

        var matchSlot = await svc.SearchAsync("20250710", new[] { "S1" });
        Assert.Single(matchSlot.Rows);
    }
}
