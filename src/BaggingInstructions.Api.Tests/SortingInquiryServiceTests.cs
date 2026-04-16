using BaggingInstructions.Api.Core;
using BaggingInstructions.Api.Entities;
using BaggingInstructions.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace BaggingInstructions.Api.Tests;

public class SortingInquiryServiceTests
{
    private static string SiCol(string customerCode, string locationCode) =>
        $"{customerCode}\u001e{locationCode}";

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

        var k = SiCol("CUST1", "LOC1");
        var allSlots = await svc.SearchAsync("20250710", Array.Empty<string>());
        Assert.Single(allSlots.Rows);
        Assert.Single(allSlots.StoreKeys);
        Assert.Equal(k, allSlots.StoreKeys[0]);
        Assert.Equal("店舗A", allSlots.StoreHeaders[k]);
        Assert.Equal("LOC1", allSlots.StoreHeaderCodes[k]);
        Assert.Equal("CUST1", allSlots.StoreHeaderDeliveryCodes[k]);
        Assert.Equal("得意先1", allSlots.StoreHeaderDeliveryNames[k]);
        Assert.Equal(3m, allSlots.StoreHeaderCapacities[k]);
        Assert.Equal(3m, allSlots.Rows[0].RatioQuantitiesByStore[k]);
        Assert.Equal(6, allSlots.Rows[0].QuantitiesByStore[k]);
        Assert.Equal("昼食", allSlots.Rows[0].FoodType);

        var filtered = await svc.SearchAsync("20250710", new[] { "OTHER" });
        Assert.Empty(filtered.Rows);

        var matchSlot = await svc.SearchAsync("20250710", new[] { "S1" });
        Assert.Single(matchSlot.Rows);
    }

    /// <summary>
    /// 得意先が2件いれば列も2件（キーは得意先＋納入場所）。納入場所マスタの有無は列数に影響しない。
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

        var k1 = SiCol("CUST1", "LOC1");
        var k2 = SiCol("CUST2", "cus0991");
        Assert.Equal(2, res.StoreKeys.Count);
        Assert.Contains(k1, res.StoreKeys);
        Assert.Contains(k2, res.StoreKeys);
        Assert.Equal("店舗１", res.StoreHeaders[k1]);
        Assert.Equal("cus0991", res.StoreHeaders[k2]);
        Assert.Equal("LOC1", res.StoreHeaderCodes[k1]);
        Assert.Equal("cus0991", res.StoreHeaderCodes[k2]);
        Assert.Equal("CUST1", res.StoreHeaderDeliveryCodes[k1]);
        Assert.Equal("CUST2", res.StoreHeaderDeliveryCodes[k2]);
        Assert.Equal("得意先Ａ", res.StoreHeaderDeliveryNames[k1]);
        Assert.Equal("得意先Ｂ", res.StoreHeaderDeliveryNames[k2]);

        Assert.Equal(2, res.Rows.Count);
        Assert.Equal(3, res.Rows.Single(r => r.ItemCode == "ITEM1").QuantitiesByStore[k1]);
        Assert.Equal(5, res.Rows.Single(r => r.ItemCode == "ITEM2").QuantitiesByStore[k2]);
    }

    /// <summary>
    /// 一方は納入場所あり・他方は受注に納入場所コードが無くても、得意先コードが違えば別列になる（納入場所キーも別）。
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

        var k3 = SiCol("CUS003", "cus0991");
        var k4 = SiCol("CUS004", "");
        Assert.Equal(2, res.StoreKeys.Count);
        Assert.Contains(k3, res.StoreKeys);
        Assert.Contains(k4, res.StoreKeys);
        Assert.Equal("cus0991", res.StoreHeaders[k3]);
        Assert.Equal("", res.StoreHeaders[k4]);
        Assert.Equal("cus0991", res.StoreHeaderCodes[k3]);
        Assert.Equal("", res.StoreHeaderCodes[k4]);
        Assert.Equal("CUS003", res.StoreHeaderDeliveryCodes[k3]);
        Assert.Equal("CUS004", res.StoreHeaderDeliveryCodes[k4]);
        Assert.Equal("四川飯店", res.StoreHeaderDeliveryNames[k3]);
        Assert.Equal("別得意先", res.StoreHeaderDeliveryNames[k4]);

        Assert.Equal(7, res.Rows.Single(r => r.ItemCode == "110").QuantitiesByStore[k3]);
        Assert.Equal(9, res.Rows.Single(r => r.ItemCode == "226").QuantitiesByStore[k4]);
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

        var ka = SiCol("C_A", "L_A");
        var kb = SiCol("C_B", "L_B");
        Assert.Equal(2, res.StoreKeys.Count);
        Assert.Equal("場所A", res.StoreHeaders[ka]);
        Assert.Equal("場所B", res.StoreHeaders[kb]);
        Assert.Equal("L_A", res.StoreHeaderCodes[ka]);
        Assert.Equal("L_B", res.StoreHeaderCodes[kb]);
        Assert.Equal("C_A", res.StoreHeaderDeliveryCodes[ka]);
        Assert.Equal("C_B", res.StoreHeaderDeliveryCodes[kb]);
        Assert.Equal("客A", res.StoreHeaderDeliveryNames[ka]);
        Assert.Equal("客B", res.StoreHeaderDeliveryNames[kb]);
        Assert.Equal(1, res.Rows.Single(r => r.ItemCode == "I1").QuantitiesByStore[ka]);
        Assert.Equal(2, res.Rows.Single(r => r.ItemCode == "I2").QuantitiesByStore[kb]);
    }

    [Fact]
    public async Task SearchAsync_Same_customer_two_locations_gets_two_columns()
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

        var kEast = SiCol("C1", "L1");
        var kWest = SiCol("C1", "L2");
        Assert.Equal(2, res.StoreKeys.Count);
        Assert.Contains(kEast, res.StoreKeys);
        Assert.Contains(kWest, res.StoreKeys);
        Assert.Equal("東", res.StoreHeaders[kEast]);
        Assert.Equal("西", res.StoreHeaders[kWest]);
        Assert.Equal("L1", res.StoreHeaderCodes[kEast]);
        Assert.Equal("L2", res.StoreHeaderCodes[kWest]);
        Assert.Equal("C1", res.StoreHeaderDeliveryCodes[kEast]);
        Assert.Equal("C1", res.StoreHeaderDeliveryCodes[kWest]);
        Assert.Equal("同一客", res.StoreHeaderDeliveryNames[kEast]);
        Assert.Equal("同一客", res.StoreHeaderDeliveryNames[kWest]);
        Assert.Equal(2, res.Rows.Single().QuantitiesByStore[kEast]);
        Assert.Equal(3, res.Rows.Single().QuantitiesByStore[kWest]);
    }
}
