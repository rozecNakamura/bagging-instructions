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
        app.Customers.Add(new Customer { CustomerCode = "CUST1", CustomerName = "得意先1" });
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
            QtyUni0 = 4,
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
            QtyUni0 = 2,
            PlannedDeliveryDate = d,
            SlotCode = "S1"
        });
        app.SalesOrderLineAddinfos.Add(new SalesOrderLineAddinfo
        {
            SalesOrderLineAddinfoId = 1,
            SalesOrderLineId = 1,
            Addinfo01 = "2",
            Addinfo02 = "FT1",
            Addinfo02Name = "昼食"
        });
        app.SalesOrderLineAddinfos.Add(new SalesOrderLineAddinfo
        {
            SalesOrderLineAddinfoId = 2,
            SalesOrderLineId = 2,
            Addinfo01 = "2",
            Addinfo02 = "FT1",
            Addinfo02Name = "昼食"
        });
        await app.SaveChangesAsync();

        var svc = new SortingInquiryService(app);

        var allSlots = await svc.SearchAsync("20250710", Array.Empty<string>());
        Assert.Single(allSlots.Rows);
        Assert.Single(allSlots.StoreKeys);
        Assert.Equal("得意先1", allSlots.StoreHeaders["CUST1"]);
        Assert.Equal("CUST1", allSlots.StoreHeaderCodes["CUST1"]);
        Assert.Equal("LOC1", allSlots.StoreHeaderDeliveryCodes["CUST1"]);
        Assert.Equal("店舗A", allSlots.StoreHeaderDeliveryNames["CUST1"]);
        Assert.Equal(3m, allSlots.StoreHeaderCapacities["CUST1"]);
        Assert.Equal(3m, allSlots.Rows[0].RatioQuantitiesByStore["CUST1"]);
        Assert.Equal(6, allSlots.Rows[0].QuantitiesByStore["CUST1"]);
        Assert.Equal("昼食", allSlots.Rows[0].FoodType);

        var filtered = await svc.SearchAsync("20250710", new[] { "OTHER" });
        Assert.Empty(filtered.Rows);

        var matchSlot = await svc.SearchAsync("20250710", new[] { "S1" });
        Assert.Single(matchSlot.Rows);
    }

    /// <summary>
    /// 得意先が2件いれば列も2件（キーは得意先コード）。納入場所マスタの有無は列数に影響しない。
    /// </summary>
    [Fact]
    public async Task SearchAsync_Uses_order_delivery_code_when_master_row_missing_separate_columns_per_customer()
    {
        await using var app = CreateAppDb();
        var d = new DateOnly(2025, 7, 10);

        app.Customers.Add(new Customer { CustomerCode = "CUST1", CustomerName = "得意先Ａ" });
        app.Customers.Add(new Customer { CustomerCode = "CUST2", CustomerName = "得意先Ｂ" });
        app.CustomerDeliveryLocations.Add(new CustomerDeliveryLocation
        {
            DeliveryLocationId = 1,
            CustomerCode = "CUST1",
            LocationCode = "LOC1",
            LocationName = "店舗１"
        });
        // CUST2: マスタ行なし。受注に cus0991 のみ。

        app.SalesOrders.Add(new SalesOrder
        {
            SalesOrderId = 1,
            CustomerCode = "CUST1",
            CustomerDeliveryLocationCode = "LOC1"
        });
        app.SalesOrders.Add(new SalesOrder
        {
            SalesOrderId = 2,
            CustomerCode = "CUST2",
            CustomerDeliveryLocationCode = "cus0991"
        });

        app.Items.Add(new Item
        {
            ItemId = 1,
            ItemCd = "ITEM1",
            ItemName = "商品１",
            ActiveFlag = true
        });
        app.Items.Add(new Item
        {
            ItemId = 2,
            ItemCd = "ITEM2",
            ItemName = "商品２",
            ActiveFlag = true
        });

        app.SalesOrderLines.Add(new SalesOrderLine
        {
            SalesOrderLineId = 1,
            SalesOrderId = 1,
            LineNo = 1,
            ItemCd = "ITEM1",
            Quantity = 3,
            PlannedDeliveryDate = d,
            SlotCode = "S1"
        });
        app.SalesOrderLines.Add(new SalesOrderLine
        {
            SalesOrderLineId = 2,
            SalesOrderId = 2,
            LineNo = 1,
            ItemCd = "ITEM2",
            Quantity = 5,
            PlannedDeliveryDate = d,
            SlotCode = "S1"
        });
        await app.SaveChangesAsync();

        var svc = new SortingInquiryService(app);
        var res = await svc.SearchAsync("20250710", Array.Empty<string>());

        Assert.Equal(2, res.StoreKeys.Count);
        Assert.Contains("CUST1", res.StoreKeys);
        Assert.Contains("CUST2", res.StoreKeys);
        Assert.Equal("得意先Ａ", res.StoreHeaders["CUST1"]);
        Assert.Equal("得意先Ｂ", res.StoreHeaders["CUST2"]);
        Assert.Equal("CUST1", res.StoreHeaderCodes["CUST1"]);
        Assert.Equal("CUST2", res.StoreHeaderCodes["CUST2"]);
        Assert.Equal("LOC1", res.StoreHeaderDeliveryCodes["CUST1"]);
        Assert.Equal("cus0991", res.StoreHeaderDeliveryCodes["CUST2"]);
        Assert.Equal("店舗１", res.StoreHeaderDeliveryNames["CUST1"]);
        Assert.Equal("cus0991", res.StoreHeaderDeliveryNames["CUST2"]);

        Assert.Equal(2, res.Rows.Count);
        Assert.Equal(3, res.Rows.Single(r => r.ItemCode == "ITEM1").QuantitiesByStore["CUST1"]);
        Assert.Equal(5, res.Rows.Single(r => r.ItemCode == "ITEM2").QuantitiesByStore["CUST2"]);
    }

    /// <summary>
    /// 一方は納入場所あり・他方は受注に納入場所コードが無くても、得意先コードが違えば別列になる。
    /// </summary>
    [Fact]
    public async Task SearchAsync_No_delivery_code_on_order_still_gets_column_per_customer()
    {
        await using var app = CreateAppDb();
        var d = new DateOnly(2026, 3, 30);

        app.Customers.Add(new Customer { CustomerCode = "CUS003", CustomerName = "四川飯店" });
        app.Customers.Add(new Customer { CustomerCode = "CUS004", CustomerName = "別得意先" });
        app.CustomerDeliveryLocations.Add(new CustomerDeliveryLocation
        {
            DeliveryLocationId = 1,
            CustomerCode = "CUS003",
            LocationCode = "cus0991",
            LocationName = ""
        });

        app.SalesOrders.Add(new SalesOrder
        {
            SalesOrderId = 1,
            CustomerCode = "CUS003",
            CustomerDeliveryLocationCode = "cus0991"
        });
        app.SalesOrders.Add(new SalesOrder
        {
            SalesOrderId = 2,
            CustomerCode = "CUS004",
            CustomerDeliveryLocationCode = null
        });

        app.Items.Add(new Item { ItemId = 1, ItemCd = "110", ItemName = "品110", ActiveFlag = true });
        app.Items.Add(new Item { ItemId = 2, ItemCd = "226", ItemName = "品226", ActiveFlag = true });

        app.SalesOrderLines.Add(new SalesOrderLine
        {
            SalesOrderLineId = 1,
            SalesOrderId = 1,
            LineNo = 1,
            ItemCd = "110",
            Quantity = 7,
            PlannedDeliveryDate = d,
            SlotCode = "S1"
        });
        app.SalesOrderLines.Add(new SalesOrderLine
        {
            SalesOrderLineId = 2,
            SalesOrderId = 2,
            LineNo = 1,
            ItemCd = "226",
            Quantity = 9,
            PlannedDeliveryDate = d,
            SlotCode = "S1"
        });
        await app.SaveChangesAsync();

        var svc = new SortingInquiryService(app);
        var res = await svc.SearchAsync("20260330", Array.Empty<string>());

        Assert.Equal(2, res.StoreKeys.Count);
        Assert.Contains("CUS003", res.StoreKeys);
        Assert.Contains("CUS004", res.StoreKeys);
        Assert.Equal("四川飯店", res.StoreHeaders["CUS003"]);
        Assert.Equal("別得意先", res.StoreHeaders["CUS004"]);
        Assert.Equal("CUS003", res.StoreHeaderCodes["CUS003"]);
        Assert.Equal("CUS004", res.StoreHeaderCodes["CUS004"]);
        Assert.Equal("cus0991", res.StoreHeaderDeliveryCodes["CUS003"]);
        Assert.Equal("", res.StoreHeaderDeliveryCodes["CUS004"]);
        Assert.Equal("cus0991", res.StoreHeaderDeliveryNames["CUS003"]);
        Assert.Equal("", res.StoreHeaderDeliveryNames["CUS004"]);

        Assert.Equal(7, res.Rows.Single(r => r.ItemCode == "110").QuantitiesByStore["CUS003"]);
        Assert.Equal(9, res.Rows.Single(r => r.ItemCode == "226").QuantitiesByStore["CUS004"]);
    }

    /// <summary>
    /// 便で絞り込んでも、受注明細の slotcode が空の行は除外しない（別得意先列が消える不具合の退行防止）。
    /// </summary>
    [Fact]
    public async Task SearchAsync_Slot_filter_includes_lines_with_blank_slotcode()
    {
        await using var app = CreateAppDb();
        var d = new DateOnly(2025, 8, 1);

        app.Customers.Add(new Customer { CustomerCode = "C_A", CustomerName = "客A" });
        app.Customers.Add(new Customer { CustomerCode = "C_B", CustomerName = "客B" });
        app.CustomerDeliveryLocations.Add(new CustomerDeliveryLocation
        {
            DeliveryLocationId = 1,
            CustomerCode = "C_A",
            LocationCode = "L_A",
            LocationName = "場所A"
        });
        app.CustomerDeliveryLocations.Add(new CustomerDeliveryLocation
        {
            DeliveryLocationId = 2,
            CustomerCode = "C_B",
            LocationCode = "L_B",
            LocationName = "場所B"
        });

        app.SalesOrders.Add(new SalesOrder
        {
            SalesOrderId = 1,
            CustomerCode = "C_A",
            CustomerDeliveryLocationCode = "L_A"
        });
        app.SalesOrders.Add(new SalesOrder
        {
            SalesOrderId = 2,
            CustomerCode = "C_B",
            CustomerDeliveryLocationCode = "L_B"
        });

        app.Items.Add(new Item { ItemId = 1, ItemCd = "I1", ItemName = "品1", ActiveFlag = true });
        app.Items.Add(new Item { ItemId = 2, ItemCd = "I2", ItemName = "品2", ActiveFlag = true });

        app.SalesOrderLines.Add(new SalesOrderLine
        {
            SalesOrderLineId = 1,
            SalesOrderId = 1,
            LineNo = 1,
            ItemCd = "I1",
            Quantity = 1,
            PlannedDeliveryDate = d,
            SlotCode = "S1"
        });
        app.SalesOrderLines.Add(new SalesOrderLine
        {
            SalesOrderLineId = 2,
            SalesOrderId = 2,
            LineNo = 1,
            ItemCd = "I2",
            Quantity = 2,
            PlannedDeliveryDate = d,
            SlotCode = null
        });
        await app.SaveChangesAsync();

        var svc = new SortingInquiryService(app);
        var res = await svc.SearchAsync("20250801", new[] { "S1" });

        Assert.Equal(2, res.StoreKeys.Count);
        Assert.Equal("客A", res.StoreHeaders["C_A"]);
        Assert.Equal("客B", res.StoreHeaders["C_B"]);
        Assert.Equal("C_A", res.StoreHeaderCodes["C_A"]);
        Assert.Equal("C_B", res.StoreHeaderCodes["C_B"]);
        Assert.Equal("L_A", res.StoreHeaderDeliveryCodes["C_A"]);
        Assert.Equal("L_B", res.StoreHeaderDeliveryCodes["C_B"]);
        Assert.Equal("場所A", res.StoreHeaderDeliveryNames["C_A"]);
        Assert.Equal("場所B", res.StoreHeaderDeliveryNames["C_B"]);
        Assert.Equal(1, res.Rows.Single(r => r.ItemCode == "I1").QuantitiesByStore["C_A"]);
        Assert.Equal(2, res.Rows.Single(r => r.ItemCode == "I2").QuantitiesByStore["C_B"]);
    }

    [Fact]
    public async Task SearchAsync_Same_customer_two_locations_sums_into_one_column()
    {
        await using var app = CreateAppDb();
        var d = new DateOnly(2025, 9, 1);

        app.Customers.Add(new Customer { CustomerCode = "C1", CustomerName = "同一客" });
        app.CustomerDeliveryLocations.Add(new CustomerDeliveryLocation
        {
            DeliveryLocationId = 1,
            CustomerCode = "C1",
            LocationCode = "L1",
            LocationName = "東"
        });
        app.CustomerDeliveryLocations.Add(new CustomerDeliveryLocation
        {
            DeliveryLocationId = 2,
            CustomerCode = "C1",
            LocationCode = "L2",
            LocationName = "西"
        });

        app.SalesOrders.Add(new SalesOrder
        {
            SalesOrderId = 1,
            CustomerCode = "C1",
            CustomerDeliveryLocationCode = "L1"
        });
        app.SalesOrders.Add(new SalesOrder
        {
            SalesOrderId = 2,
            CustomerCode = "C1",
            CustomerDeliveryLocationCode = "L2"
        });

        app.Items.Add(new Item { ItemId = 1, ItemCd = "X", ItemName = "品X", ActiveFlag = true });

        app.SalesOrderLines.Add(new SalesOrderLine
        {
            SalesOrderLineId = 1,
            SalesOrderId = 1,
            LineNo = 1,
            ItemCd = "X",
            Quantity = 2,
            PlannedDeliveryDate = d,
            SlotCode = "S1"
        });
        app.SalesOrderLines.Add(new SalesOrderLine
        {
            SalesOrderLineId = 2,
            SalesOrderId = 2,
            LineNo = 1,
            ItemCd = "X",
            Quantity = 3,
            PlannedDeliveryDate = d,
            SlotCode = "S1"
        });
        await app.SaveChangesAsync();

        var svc = new SortingInquiryService(app);
        var res = await svc.SearchAsync("20250901", Array.Empty<string>());

        Assert.Single(res.StoreKeys);
        Assert.Equal("C1", res.StoreKeys[0]);
        Assert.Equal("同一客", res.StoreHeaders["C1"]);
        Assert.Equal("C1", res.StoreHeaderCodes["C1"]);
        Assert.Equal("L1／L2", res.StoreHeaderDeliveryCodes["C1"]);
        Assert.Equal("東／西", res.StoreHeaderDeliveryNames["C1"]);
        Assert.Equal(5, res.Rows.Single().QuantitiesByStore["C1"]);
    }
}
