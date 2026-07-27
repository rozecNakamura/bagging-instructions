using BaggingInstructions.Api.Core;
using BaggingInstructions.Api.Entities;
using BaggingInstructions.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace BaggingInstructions.Api.Tests;

public class SortingInquiryServiceTests
{
    private static string SiCol(string customerCode, string locationCode) =>
        $"{customerCode}{locationCode}";

    private static AppDbContext CreateAppDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static CstmeatDbContext CreateCstmeatDb()
    {
        var options = new DbContextOptionsBuilder<CstmeatDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new CstmeatDbContext(options);
    }

    /// <summary>
    /// 仕分け照会の食数照合用に確定(info22="1")の cstmeat 行を追加する。
    /// キーは (得意先=info01, 納入場所=info02, 喫食時間=info04, 食種=info05)。
    /// 明細側のキーは (SalesOrder.CustomerCode, 納入場所, Addinfo05, Addinfo02) に対応する。
    /// </summary>
    private static void AddCstmeat(
        CstmeatDbContext cstmeat, int id, string date,
        string cust, string loc, string? mealTime, string? foodType, decimal qty)
    {
        cstmeat.Cstmeats.Add(new Cstmeat
        {
            CstmeatId = id,
            Info01 = cust,
            Info02 = loc,
            Info03 = date,
            Info04 = mealTime,
            Info05 = foodType,
            Info07 = qty.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Info22 = "1"
        });
    }

    [Fact]
    public async Task SearchAsync_InvalidDate_ThrowsArgumentException()
    {
        await using var app = CreateAppDb();
        await using var cstmeat = CreateCstmeatDb();
        var svc = new SortingInquiryService(app, cstmeat);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.SearchAsync("2024", null, null, CancellationToken.None));
    }

    [Fact]
    public async Task SearchAsync_Filters_by_slot_and_aggregates_by_item_foodtype_and_store()
    {
        await using var app = CreateAppDb();
        await using var cstmeat = CreateCstmeatDb();

        var d = new DateOnly(2025, 7, 10);
        app.Customers.Add(new Customer { CustomerCode = "200", CustomerName = "得意先1" });
        app.CustomerDeliveryLocations.Add(new CustomerDeliveryLocation
        {
            DeliveryLocationId = 1,
            CustomerCode = "200",
            LocationCode = "LOC1",
            LocationName = "店舗A"
        });
        app.SalesOrders.Add(new SalesOrder
        {
            Status = "confirmed",
            SalesOrderId = 1,
            CustomerCode = "200",
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

        AddCstmeat(cstmeat, 1, "20250710", "200", "LOC1", null, "FT1", 6m);
        await cstmeat.SaveChangesAsync();

        var svc = new SortingInquiryService(app, cstmeat);

        var k = SiCol("200", "LOC1");
        var allSlots = await svc.SearchAsync("20250710", Array.Empty<string>());
        Assert.Single(allSlots.Rows);
        Assert.Single(allSlots.StoreKeys);
        Assert.Equal(k, allSlots.StoreKeys[0]);
        Assert.Equal("店舗A", allSlots.StoreHeaders[k]);
        Assert.Equal("LOC1", allSlots.StoreHeaderCodes[k]);
        Assert.Equal("200", allSlots.StoreHeaderDeliveryCodes[k]);
        Assert.Equal("得意先1", allSlots.StoreHeaderDeliveryNames[k]);
        Assert.Equal(6m, allSlots.Rows[0].QuantitiesByStore[k]);
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
        await using var cstmeat = CreateCstmeatDb();
        var d = new DateOnly(2025, 7, 10);

        app.Customers.Add(new Customer { CustomerCode = "200", CustomerName = "得意先Ａ" });
        app.Customers.Add(new Customer { CustomerCode = "210", CustomerName = "得意先Ｂ" });
        app.CustomerDeliveryLocations.Add(new CustomerDeliveryLocation
        {
            DeliveryLocationId = 1,
            CustomerCode = "200",
            LocationCode = "LOC1",
            LocationName = "店舗１"
        });
        // 210: マスタ行なし。受注に cus0991 のみ。

        app.SalesOrders.Add(new SalesOrder
        {
            Status = "confirmed",
            SalesOrderId = 1,
            CustomerCode = "200",
            CustomerDeliveryLocationCode = "LOC1"
        });
        app.SalesOrders.Add(new SalesOrder
        {
            Status = "confirmed",
            SalesOrderId = 2,
            CustomerCode = "210",
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

        AddCstmeat(cstmeat, 1, "20250710", "200", "LOC1", null, null, 3m);
        AddCstmeat(cstmeat, 2, "20250710", "210", "cus0991", null, null, 5m);
        await cstmeat.SaveChangesAsync();

        var svc = new SortingInquiryService(app, cstmeat);
        var res = await svc.SearchAsync("20250710", Array.Empty<string>());

        var k1 = SiCol("200", "LOC1");
        var k2 = SiCol("210", "cus0991");
        Assert.Equal(2, res.StoreKeys.Count);
        Assert.Contains(k1, res.StoreKeys);
        Assert.Contains(k2, res.StoreKeys);
        Assert.Equal("店舗１", res.StoreHeaders[k1]);
        Assert.Equal("cus0991", res.StoreHeaders[k2]);
        Assert.Equal("LOC1", res.StoreHeaderCodes[k1]);
        Assert.Equal("cus0991", res.StoreHeaderCodes[k2]);
        Assert.Equal("200", res.StoreHeaderDeliveryCodes[k1]);
        Assert.Equal("210", res.StoreHeaderDeliveryCodes[k2]);
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
        await using var cstmeat = CreateCstmeatDb();
        var d = new DateOnly(2026, 3, 30);

        app.Customers.Add(new Customer { CustomerCode = "200", CustomerName = "四川飯店" });
        app.Customers.Add(new Customer { CustomerCode = "210", CustomerName = "別得意先" });
        app.CustomerDeliveryLocations.Add(new CustomerDeliveryLocation
        {
            DeliveryLocationId = 1,
            CustomerCode = "200",
            LocationCode = "cus0991",
            LocationName = ""
        });

        app.SalesOrders.Add(new SalesOrder
        {
            Status = "confirmed",
            SalesOrderId = 1,
            CustomerCode = "200",
            CustomerDeliveryLocationCode = "cus0991"
        });
        app.SalesOrders.Add(new SalesOrder
        {
            Status = "confirmed",
            SalesOrderId = 2,
            CustomerCode = "210",
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

        AddCstmeat(cstmeat, 1, "20260330", "200", "cus0991", null, null, 7m);
        AddCstmeat(cstmeat, 2, "20260330", "210", "", null, null, 9m);
        await cstmeat.SaveChangesAsync();

        var svc = new SortingInquiryService(app, cstmeat);
        var res = await svc.SearchAsync("20260330", Array.Empty<string>());

        var k3 = SiCol("200", "cus0991");
        var k4 = SiCol("210", "");
        Assert.Equal(2, res.StoreKeys.Count);
        Assert.Contains(k3, res.StoreKeys);
        Assert.Contains(k4, res.StoreKeys);
        Assert.Equal("cus0991", res.StoreHeaders[k3]);
        Assert.Equal("", res.StoreHeaders[k4]);
        Assert.Equal("cus0991", res.StoreHeaderCodes[k3]);
        Assert.Equal("", res.StoreHeaderCodes[k4]);
        Assert.Equal("200", res.StoreHeaderDeliveryCodes[k3]);
        Assert.Equal("210", res.StoreHeaderDeliveryCodes[k4]);
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
        await using var cstmeat = CreateCstmeatDb();
        var d = new DateOnly(2025, 8, 1);

        app.Customers.Add(new Customer { CustomerCode = "200", CustomerName = "客A" });
        app.Customers.Add(new Customer { CustomerCode = "210", CustomerName = "客B" });
        app.CustomerDeliveryLocations.Add(new CustomerDeliveryLocation
        {
            DeliveryLocationId = 1,
            CustomerCode = "200",
            LocationCode = "L_A",
            LocationName = "場所A"
        });
        app.CustomerDeliveryLocations.Add(new CustomerDeliveryLocation
        {
            DeliveryLocationId = 2,
            CustomerCode = "210",
            LocationCode = "L_B",
            LocationName = "場所B"
        });

        app.SalesOrders.Add(new SalesOrder
        {
            Status = "confirmed",
            SalesOrderId = 1,
            CustomerCode = "200",
            CustomerDeliveryLocationCode = "L_A"
        });
        app.SalesOrders.Add(new SalesOrder
        {
            Status = "confirmed",
            SalesOrderId = 2,
            CustomerCode = "210",
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

        AddCstmeat(cstmeat, 1, "20250801", "200", "L_A", null, null, 1m);
        AddCstmeat(cstmeat, 2, "20250801", "210", "L_B", null, null, 2m);
        await cstmeat.SaveChangesAsync();

        var svc = new SortingInquiryService(app, cstmeat);
        var res = await svc.SearchAsync("20250801", new[] { "S1" });

        var ka = SiCol("200", "L_A");
        var kb = SiCol("210", "L_B");
        Assert.Equal(2, res.StoreKeys.Count);
        Assert.Equal("場所A", res.StoreHeaders[ka]);
        Assert.Equal("場所B", res.StoreHeaders[kb]);
        Assert.Equal("L_A", res.StoreHeaderCodes[ka]);
        Assert.Equal("L_B", res.StoreHeaderCodes[kb]);
        Assert.Equal("200", res.StoreHeaderDeliveryCodes[ka]);
        Assert.Equal("210", res.StoreHeaderDeliveryCodes[kb]);
        Assert.Equal("客A", res.StoreHeaderDeliveryNames[ka]);
        Assert.Equal("客B", res.StoreHeaderDeliveryNames[kb]);
        Assert.Equal(1, res.Rows.Single(r => r.ItemCode == "I1").QuantitiesByStore[ka]);
        Assert.Equal(2, res.Rows.Single(r => r.ItemCode == "I2").QuantitiesByStore[kb]);
    }

    [Fact]
    public async Task SearchAsync_Same_customer_two_locations_gets_two_columns()
    {
        await using var app = CreateAppDb();
        await using var cstmeat = CreateCstmeatDb();
        var d = new DateOnly(2025, 9, 1);

        app.Customers.Add(new Customer { CustomerCode = "200", CustomerName = "同一客" });
        app.CustomerDeliveryLocations.Add(new CustomerDeliveryLocation
        {
            DeliveryLocationId = 1,
            CustomerCode = "200",
            LocationCode = "L1",
            LocationName = "東"
        });
        app.CustomerDeliveryLocations.Add(new CustomerDeliveryLocation
        {
            DeliveryLocationId = 2,
            CustomerCode = "200",
            LocationCode = "L2",
            LocationName = "西"
        });

        app.SalesOrders.Add(new SalesOrder
        {
            Status = "confirmed",
            SalesOrderId = 1,
            CustomerCode = "200",
            CustomerDeliveryLocationCode = "L1"
        });
        app.SalesOrders.Add(new SalesOrder
        {
            Status = "confirmed",
            SalesOrderId = 2,
            CustomerCode = "200",
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

        AddCstmeat(cstmeat, 1, "20250901", "200", "L1", null, null, 2m);
        AddCstmeat(cstmeat, 2, "20250901", "200", "L2", null, null, 3m);
        await cstmeat.SaveChangesAsync();

        var svc = new SortingInquiryService(app, cstmeat);
        var res = await svc.SearchAsync("20250901", Array.Empty<string>());

        var kEast = SiCol("200", "L1");
        var kWest = SiCol("200", "L2");
        Assert.Equal(2, res.StoreKeys.Count);
        Assert.Contains(kEast, res.StoreKeys);
        Assert.Contains(kWest, res.StoreKeys);
        Assert.Equal("東", res.StoreHeaders[kEast]);
        Assert.Equal("西", res.StoreHeaders[kWest]);
        Assert.Equal("L1", res.StoreHeaderCodes[kEast]);
        Assert.Equal("L2", res.StoreHeaderCodes[kWest]);
        Assert.Equal("200", res.StoreHeaderDeliveryCodes[kEast]);
        Assert.Equal("200", res.StoreHeaderDeliveryCodes[kWest]);
        Assert.Equal("同一客", res.StoreHeaderDeliveryNames[kEast]);
        Assert.Equal("同一客", res.StoreHeaderDeliveryNames[kWest]);
        Assert.Equal(2, res.Rows.Single().QuantitiesByStore[kEast]);
        Assert.Equal(3, res.Rows.Single().QuantitiesByStore[kWest]);
    }

    /// <summary>
    /// 同一品目に複数食種がある場合、priority_order が最小の食種が代表食種として選ばれる。
    /// priority_order 未登録の食種は後回し（int.MaxValue 扱い）。
    /// </summary>
    [Fact]
    public async Task SearchAsync_Representative_foodtype_selected_by_lowest_priority_order()
    {
        await using var app = CreateAppDb();
        await using var cstmeat = CreateCstmeatDb();

        // FT_A: priority_order=2, FT_B: priority_order=1 → 代表は FT_B
        cstmeat.Mshokushus.Add(new Mshokushu { Id = 1, ShokushuCode = "FT_A", PriorityOrder = 2 });
        cstmeat.Mshokushus.Add(new Mshokushu { Id = 2, ShokushuCode = "FT_B", PriorityOrder = 1 });
        await cstmeat.SaveChangesAsync();

        var d = new DateOnly(2025, 10, 1);
        app.Customers.Add(new Customer { CustomerCode = "200", CustomerName = "施設X" });
        app.CustomerDeliveryLocations.Add(new CustomerDeliveryLocation
        {
            DeliveryLocationId = 1,
            CustomerCode = "200",
            LocationCode = "LOC1",
            LocationName = "拠点1"
        });
        app.SalesOrders.Add(new SalesOrder
        {
            Status = "confirmed",
            SalesOrderId = 1,
            CustomerCode = "200",
            CustomerDeliveryLocationCode = "LOC1"
        });
        app.Items.Add(new Item { ItemId = 1, ItemCd = "ITEM1", ItemName = "商品1", ActiveFlag = true });

        // 同じ品目に食種 FT_A と FT_B の2明細
        app.SalesOrderLines.Add(new SalesOrderLine
        {
            SalesOrderLineId = 1,
            SalesOrderId = 1,
            LineNo = 1,
            ItemCd = "ITEM1",
            Quantity = 10,
            PlannedDeliveryDate = d,
            SlotCode = "S1"
        });
        app.SalesOrderLines.Add(new SalesOrderLine
        {
            SalesOrderLineId = 2,
            SalesOrderId = 1,
            LineNo = 2,
            ItemCd = "ITEM1",
            Quantity = 5,
            PlannedDeliveryDate = d,
            SlotCode = "S1"
        });
        app.SalesOrderLineAddinfos.Add(new SalesOrderLineAddinfo
        {
            SalesOrderLineAddinfoId = 1,
            SalesOrderLineId = 1,
            Addinfo02 = "FT_A",
            Addinfo02Name = "夕食"
        });
        app.SalesOrderLineAddinfos.Add(new SalesOrderLineAddinfo
        {
            SalesOrderLineAddinfoId = 2,
            SalesOrderLineId = 2,
            Addinfo02 = "FT_B",
            Addinfo02Name = "昼食"
        });
        await app.SaveChangesAsync();

        AddCstmeat(cstmeat, 1, "20251001", "200", "LOC1", null, "FT_A", 10m);
        AddCstmeat(cstmeat, 2, "20251001", "200", "LOC1", null, "FT_B", 5m);
        await cstmeat.SaveChangesAsync();

        var svc = new SortingInquiryService(app, cstmeat);
        var res = await svc.SearchAsync("20251001", Array.Empty<string>());

        // 品目単位で集約されるので1行
        Assert.Single(res.Rows);
        var row = res.Rows[0];
        Assert.Equal("ITEM1", row.ItemCode);
        // priority_order=1 の FT_B（昼食）が代表食種
        Assert.Equal("昼食", row.FoodType);
        // 数量は両食種の合計
        var k = SiCol("200", "LOC1");
        Assert.Equal(15, row.QuantitiesByStore[k]);
    }

    /// <summary>
    /// cstmeat 食数は (得意先・納入場所・喫食時間・食種) 単位の合計値なので、
    /// 同一品目・同一食種の明細が複数あっても 1 回だけ計上する（明細本数ぶんの多重計上をしない）。
    /// また確定レコード（info22="1"）のみを対象とし、予定（"0"）・取消（"9"）・NULL は集計対象外とする。
    /// </summary>
    [Fact]
    public async Task SearchAsync_Cstmeat_food_count_counted_once_per_key_and_confirmed_only()
    {
        await using var app = CreateAppDb();
        await using var cstmeat = CreateCstmeatDb();

        var d = new DateOnly(2026, 7, 20);

        // 確定: (200, LOC1, 朝=1, FT1) の食数 = 10
        cstmeat.Cstmeats.Add(new Cstmeat
        {
            CstmeatId = 1,
            Info01 = "200", Info02 = "LOC1", Info03 = "20260720",
            Info04 = "1", Info05 = "FT1", Info06 = "1", Info07 = "10",
            Info22 = "1"
        });
        // 取消: 同一キーに食数 99 があるが info22="9" のため除外される
        cstmeat.Cstmeats.Add(new Cstmeat
        {
            CstmeatId = 2,
            Info01 = "200", Info02 = "LOC1", Info03 = "20260720",
            Info04 = "1", Info05 = "FT1", Info06 = "1", Info07 = "99",
            Info22 = "9"
        });
        // 予定: 同一キーに食数 77 があるが info22="0"（予定）のため除外される
        cstmeat.Cstmeats.Add(new Cstmeat
        {
            CstmeatId = 3,
            Info01 = "200", Info02 = "LOC1", Info03 = "20260720",
            Info04 = "1", Info05 = "FT1", Info06 = "1", Info07 = "77",
            Info22 = "0"
        });
        await cstmeat.SaveChangesAsync();

        app.Customers.Add(new Customer { CustomerCode = "200", CustomerName = "越智クリニック" });
        app.CustomerDeliveryLocations.Add(new CustomerDeliveryLocation
        {
            DeliveryLocationId = 1,
            CustomerCode = "200",
            LocationCode = "LOC1",
            LocationName = "越智クリニック"
        });
        app.SalesOrders.Add(new SalesOrder
        {
            Status = "confirmed",
            SalesOrderId = 1,
            CustomerCode = "200",
            CustomerDeliveryLocationCode = "LOC1"
        });
        app.Items.Add(new Item { ItemId = 1, ItemCd = "ITEM1", ItemName = "茹・ブロッコリー冷", ActiveFlag = true });

        // 同一品目・同一食種(FT1)・同一喫食時間の明細が2本 → cstmeat 食数(10)は1回だけ計上
        app.SalesOrderLines.Add(new SalesOrderLine
        {
            SalesOrderLineId = 1, SalesOrderId = 1, LineNo = 1,
            ItemCd = "ITEM1", Quantity = 4, QtyUni0 = 4,
            PlannedDeliveryDate = d, SlotCode = "S1"
        });
        app.SalesOrderLines.Add(new SalesOrderLine
        {
            SalesOrderLineId = 2, SalesOrderId = 1, LineNo = 2,
            ItemCd = "ITEM1", Quantity = 2, QtyUni0 = 2,
            PlannedDeliveryDate = d, SlotCode = "S1"
        });
        app.SalesOrderLineAddinfos.Add(new SalesOrderLineAddinfo
        {
            SalesOrderLineAddinfoId = 1, SalesOrderLineId = 1,
            Addinfo02 = "FT1", Addinfo02Name = "常菜", Addinfo05 = "1"
        });
        app.SalesOrderLineAddinfos.Add(new SalesOrderLineAddinfo
        {
            SalesOrderLineAddinfoId = 2, SalesOrderLineId = 2,
            Addinfo02 = "FT1", Addinfo02Name = "常菜", Addinfo05 = "1"
        });
        await app.SaveChangesAsync();

        var svc = new SortingInquiryService(app, cstmeat);
        var res = await svc.SearchAsync("20260720", Array.Empty<string>());

        var k = SiCol("200", "LOC1");
        Assert.Single(res.Rows);
        // cstmeat 食数 10 を1回だけ計上（明細2本ぶんの 20 でも、取消込みの 109 でもない）。
        Assert.Equal(10m, res.Rows[0].QuantitiesByStore[k]);
    }
}
