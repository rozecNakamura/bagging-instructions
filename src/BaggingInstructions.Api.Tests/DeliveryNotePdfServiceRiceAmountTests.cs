using BaggingInstructions.Api.Core;
using BaggingInstructions.Api.Entities;
using BaggingInstructions.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace BaggingInstructions.Api.Tests;

/// <summary>
/// 個人配食（300）の納品書：備考のご飯量（addinfo01）。
/// 同一喫食日に予定（draft/open）受注と確定（confirmed）受注が並存する場合、
/// 明細の食種（cstmeat.info05 = salesorderlineaddinfo.addinfo02）で突合し、
/// 食種で引けない場合は確定受注を優先する。RECEPTNO も確定受注を優先する。
/// </summary>
public class DeliveryNotePdfServiceRiceAmountTests
{
    private const string EatingDate = "20260912";
    private const string CustomerCode = "300";
    private const string LocationCode = "027780";
    private const string RiceItemCd = "3010100000";
    private const string MealTimeLunch = "2";

    /// <summary>予定側：食種 5AE（糖尿）/ ご飯量 160g / 受注明細は open</summary>
    private const string PlannedShokushu = "5AE";

    /// <summary>確定側：食種 5AA（常菜）/ ご飯量 200g / 受注明細は confirmed</summary>
    private const string ConfirmedShokushu = "5AA";

    private const long PlannedOrderId = 262402;
    private const long PlannedLineId = 1042750;
    private const long ConfirmedOrderId = 331092;
    private const long ConfirmedLineId = 1111999;

    private static readonly DateOnly DeliveryDate = new(2026, 9, 12);

    private static AppDbContext NewAppDb(string name) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase($"{name}-app").Options);

    private static CstmeatDbContext NewCstmeatDb(string name) =>
        new(new DbContextOptionsBuilder<CstmeatDbContext>().UseInMemoryDatabase($"{name}-cstmeat").Options);

    private static Cstmeat NewCstmeat(int id, string shokushuCode) => new()
    {
        CstmeatId = id,
        Info01 = CustomerCode,
        Info02 = LocationCode,
        Info03 = EatingDate,
        Info04 = MealTimeLunch,
        Info05 = shokushuCode,
        Info06 = "1",
        Info07 = "1",
        Info08 = "620",
        Info11 = "620",
        Info18 = EatingDate,
        Info19 = "2",
        Info22 = "1"   // CstmeatDbContext のグローバルフィルタ（有効データ）
    };

    /// <summary>1受注＝1明細の構成（本番同様）。lineStatus が open の受注は確定明細を持たない。</summary>
    private static SalesOrder NewOrder(long orderId, long lineId, string orderStatus, string lineStatus,
        string shokushu, string riceAmount)
    {
        var order = new SalesOrder
        {
            SalesOrderId = orderId,
            CustomerCode = CustomerCode,
            CustomerDeliveryLocationCode = LocationCode,
            Status = orderStatus
        };
        var line = new SalesOrderLine
        {
            SalesOrderLineId = lineId,
            SalesOrderId = orderId,
            SalesOrder = order,
            ItemCd = RiceItemCd,
            PlannedDeliveryDate = DeliveryDate,
            Status = lineStatus,
            Addinfo = new SalesOrderLineAddinfo
            {
                SalesOrderLineId = lineId,
                Addinfo01 = riceAmount,
                Addinfo02 = shokushu,
                Addinfo05 = MealTimeLunch,
                Addinfo07 = "order1"
            }
        };
        order.SalesOrderLines.Add(line);
        return order;
    }

    /// <summary>予定・確定の投入順を入れ替えても結果が変わらないことを確認するため、順序を引数で指定する。</summary>
    private static Dictionary<string, string> BuildSinglePage(bool confirmedFirst)
    {
        var dbName = Guid.NewGuid().ToString();
        using var appDb = NewAppDb(dbName);
        using var cstmeatDb = NewCstmeatDb(dbName);

        cstmeatDb.Mshokushus.AddRange(
            new Mshokushu { Id = 1, ShokushuCode = ConfirmedShokushu, ShokushuName = "常菜" },
            new Mshokushu { Id = 2, ShokushuCode = PlannedShokushu, ShokushuName = "糖尿" });
        cstmeatDb.Eattimes.Add(new Eattime { Eattimecd = MealTimeLunch, Eattimename = "昼食" });
        cstmeatDb.Cstmeats.AddRange(
            NewCstmeat(1, PlannedShokushu),
            NewCstmeat(2, ConfirmedShokushu));
        cstmeatDb.SaveChanges();

        appDb.Items.Add(new Item { ItemId = 1, ItemCd = RiceItemCd, ItemName = "御飯" });
        var planned = NewOrder(PlannedOrderId, PlannedLineId, "draft", "open", PlannedShokushu, "160");
        var confirmed = NewOrder(ConfirmedOrderId, ConfirmedLineId, "confirmed", "confirmed", ConfirmedShokushu, "200");
        if (confirmedFirst)
            appDb.SalesOrders.AddRange(confirmed, planned);
        else
            appDb.SalesOrders.AddRange(planned, confirmed);
        appDb.SaveChanges();

        var service = new DeliveryNotePdfService(cstmeatDb, appDb, new JuicePdfService());
        var pages = service.BuildTagValuesPagesForOne(EatingDate, LocationCode, CustomerCode, "2");
        return Assert.Single(pages);
    }

    /// <summary>明細名（ITEMNM_0_nn）が itemNm の行の備考（NOTE_0_nn）を返す。</summary>
    private static string NoteOf(Dictionary<string, string> page, string itemNm)
    {
        for (var i = 0; i < 4; i++)
        {
            var nn = i.ToString("D2");
            if (page[$"ITEMNM_0_{nn}"] == itemNm)
                return page[$"NOTE_0_{nn}"];
        }
        throw new Xunit.Sdk.XunitException($"明細 '{itemNm}' が見つかりません。");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Rice_amount_is_taken_from_the_matching_shokushu(bool confirmedFirst)
    {
        var page = BuildSinglePage(confirmedFirst);

        // 確定（常菜 5AA）の明細には確定の 200g、予定（糖尿 5AE）の明細には予定の 160g が付く
        Assert.Equal("200g", NoteOf(page, "常菜:昼喫食"));
        Assert.Equal("160g", NoteOf(page, "糖尿:昼喫食"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Receptno_prefers_the_confirmed_sales_order(bool confirmedFirst)
    {
        var page = BuildSinglePage(confirmedFirst);

        // 受注IDは予定(262402) < 確定(331092) だが、確定受注を優先する
        Assert.Equal(ConfirmedOrderId.ToString(), page["RECEPTNO_0"]);
    }

    /// <summary>
    /// cstmeat の食種が受注明細の addinfo02 と一致しない場合は喫食時間のみで引く。
    /// このフォールバックでも確定（confirmed）を優先し、DB の返却順に依存しない。
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Rice_amount_falls_back_to_meal_time_and_prefers_confirmed(bool confirmedFirst)
    {
        var dbName = Guid.NewGuid().ToString();
        using var appDb = NewAppDb(dbName);
        using var cstmeatDb = NewCstmeatDb(dbName);

        cstmeatDb.Mshokushus.Add(new Mshokushu { Id = 1, ShokushuCode = "9ZZ", ShokushuName = "常菜" });
        cstmeatDb.Eattimes.Add(new Eattime { Eattimecd = MealTimeLunch, Eattimename = "昼食" });
        cstmeatDb.Cstmeats.Add(NewCstmeat(1, "9ZZ"));   // 受注の addinfo02（5AE/5AA）と一致しない食種
        cstmeatDb.SaveChanges();

        appDb.Items.Add(new Item { ItemId = 1, ItemCd = RiceItemCd, ItemName = "御飯" });
        var planned = NewOrder(PlannedOrderId, PlannedLineId, "draft", "open", PlannedShokushu, "160");
        var confirmed = NewOrder(ConfirmedOrderId, ConfirmedLineId, "confirmed", "confirmed", ConfirmedShokushu, "200");
        if (confirmedFirst)
            appDb.SalesOrders.AddRange(confirmed, planned);
        else
            appDb.SalesOrders.AddRange(planned, confirmed);
        appDb.SaveChanges();

        var service = new DeliveryNotePdfService(cstmeatDb, appDb, new JuicePdfService());
        var page = Assert.Single(service.BuildTagValuesPagesForOne(EatingDate, LocationCode, CustomerCode, "2"));

        Assert.Equal("200g", NoteOf(page, "常菜:昼喫食"));
    }
}
