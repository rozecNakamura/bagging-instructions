using BaggingInstructions.Api.Core;
using BaggingInstructions.Api.Entities;
using BaggingInstructions.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace BaggingInstructions.Api.Tests;

/// <summary>
/// 納品書検索の並び順：納品便（info19）に応じて customerdeliverylocationaddinfo の
/// コース・配送順（朝=01/02、昼=03/04、夜=05/06）で昇順ソートする。
/// </summary>
public class DeliveryNoteServiceSortTests
{
    private const string EatingDate = "20260810";
    private const string CustomerCode = "200";

    private static AppDbContext NewAppDb(string name)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"{name}-app")
            .Options;
        return new AppDbContext(options);
    }

    private static CstmeatDbContext NewCstmeatDb(string name)
    {
        var options = new DbContextOptionsBuilder<CstmeatDbContext>()
            .UseInMemoryDatabase($"{name}-cstmeat")
            .Options;
        return new CstmeatDbContext(options);
    }

    /// <summary>納入場所と、朝/昼/夜それぞれのコース・配送順を1件登録する。</summary>
    private static async Task AddLocationAsync(
        AppDbContext appDb, long id, string locationCode, string locationName,
        string? morningCourse, string? morningOrder,
        string? noonCourse, string? noonOrder,
        string? nightCourse, string? nightOrder)
    {
        // customerdeliverylocation は customer と結合して取得されるため得意先も登録する
        if (!await appDb.Customers.AnyAsync(c => c.CustomerCode == CustomerCode))
            appDb.Customers.Add(new Customer { CustomerCode = CustomerCode, CustomerName = "得意先200" });

        appDb.CustomerDeliveryLocations.Add(new CustomerDeliveryLocation
        {
            DeliveryLocationId = id,
            CustomerCode = CustomerCode,
            LocationCode = locationCode,
            LocationName = locationName
        });
        if (morningCourse != null || morningOrder != null ||
            noonCourse != null || noonOrder != null ||
            nightCourse != null || nightOrder != null)
        {
            appDb.CustomerDeliveryLocationAddinfos.Add(new CustomerDeliveryLocationAddinfo
            {
                AddinfoId = id,
                CustomerCode = CustomerCode,
                LocationCode = locationCode,
                Addinfo01 = morningCourse,
                Addinfo02 = morningOrder,
                Addinfo03 = noonCourse,
                Addinfo04 = noonOrder,
                Addinfo05 = nightCourse,
                Addinfo06 = nightOrder
            });
        }
        await appDb.SaveChangesAsync();
    }

    private static async Task AddCstmeatAsync(CstmeatDbContext db, int id, string locationCode, string info19)
    {
        db.Cstmeats.Add(new Cstmeat
        {
            CstmeatId = id,
            Info01 = CustomerCode,
            Info02 = locationCode,
            Info07 = "10",
            Info18 = EatingDate,
            Info19 = info19,
            Info22 = "1"   // CstmeatDbContext のグローバルフィルタ（有効データ）
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Search_orders_by_addinfo01_02_for_morning_route()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var appDb = NewAppDb(dbName);
        await using var cstmeatDb = NewCstmeatDb(dbName);

        // 朝(01/02) と 昼(03/04) で並び順が逆になるデータ
        await AddLocationAsync(appDb, 1, "L1", "場所1", "A", "2", "B", "1", "C", "3");
        await AddLocationAsync(appDb, 2, "L2", "場所2", "A", "1", "B", "2", "C", "2");
        await AddCstmeatAsync(cstmeatDb, 1, "L1", "1");
        await AddCstmeatAsync(cstmeatDb, 2, "L2", "1");

        var service = new DeliveryNoteService(cstmeatDb, appDb);
        var result = await service.SearchByEatingDateAsync(EatingDate, "catering", "1");

        Assert.Equal(new[] { "場所2", "場所1" }, result.Select(x => x.LocationName));
    }

    [Fact]
    public async Task Search_orders_by_addinfo03_04_for_noon_route()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var appDb = NewAppDb(dbName);
        await using var cstmeatDb = NewCstmeatDb(dbName);

        await AddLocationAsync(appDb, 1, "L1", "場所1", "A", "2", "B", "1", "C", "3");
        await AddLocationAsync(appDb, 2, "L2", "場所2", "A", "1", "B", "2", "C", "2");
        await AddCstmeatAsync(cstmeatDb, 1, "L1", "2");
        await AddCstmeatAsync(cstmeatDb, 2, "L2", "2");

        var service = new DeliveryNoteService(cstmeatDb, appDb);
        var result = await service.SearchByEatingDateAsync(EatingDate, "catering", "2");

        Assert.Equal(new[] { "場所1", "場所2" }, result.Select(x => x.LocationName));
    }

    [Fact]
    public async Task Search_orders_by_addinfo05_06_for_night_route()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var appDb = NewAppDb(dbName);
        await using var cstmeatDb = NewCstmeatDb(dbName);

        await AddLocationAsync(appDb, 1, "L1", "場所1", "A", "2", "B", "1", "C", "3");
        await AddLocationAsync(appDb, 2, "L2", "場所2", "A", "1", "B", "2", "C", "2");
        await AddCstmeatAsync(cstmeatDb, 1, "L1", "3");
        await AddCstmeatAsync(cstmeatDb, 2, "L2", "3");

        var service = new DeliveryNoteService(cstmeatDb, appDb);
        var result = await service.SearchByEatingDateAsync(EatingDate, "catering", "3");

        Assert.Equal(new[] { "場所2", "場所1" }, result.Select(x => x.LocationName));
    }

    [Fact]
    public async Task Search_compares_delivery_order_numerically()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var appDb = NewAppDb(dbName);
        await using var cstmeatDb = NewCstmeatDb(dbName);

        // 文字列比較なら "10" < "2" になるが、数値比較のため 2 → 10 の順
        await AddLocationAsync(appDb, 1, "L1", "場所1", "A", "10", null, null, null, null);
        await AddLocationAsync(appDb, 2, "L2", "場所2", "A", "2", null, null, null, null);
        await AddCstmeatAsync(cstmeatDb, 1, "L1", "1");
        await AddCstmeatAsync(cstmeatDb, 2, "L2", "1");

        var service = new DeliveryNoteService(cstmeatDb, appDb);
        var result = await service.SearchByEatingDateAsync(EatingDate, "catering", "1");

        Assert.Equal(new[] { "場所2", "場所1" }, result.Select(x => x.LocationName));
    }

    [Fact]
    public async Task Search_places_rows_without_addinfo_last()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var appDb = NewAppDb(dbName);
        await using var cstmeatDb = NewCstmeatDb(dbName);

        // addinfo なし / 該当便のみ空 / 設定あり
        await AddLocationAsync(appDb, 1, "L1", "場所1", null, null, null, null, null, null);
        await AddLocationAsync(appDb, 2, "L2", "場所2", "", "", "B", "1", null, null);
        await AddLocationAsync(appDb, 3, "L3", "場所3", "A", "5", null, null, null, null);
        await AddCstmeatAsync(cstmeatDb, 1, "L1", "1");
        await AddCstmeatAsync(cstmeatDb, 2, "L2", "1");
        await AddCstmeatAsync(cstmeatDb, 3, "L3", "1");

        var service = new DeliveryNoteService(cstmeatDb, appDb);
        var result = await service.SearchByEatingDateAsync(EatingDate, "catering", "1");

        // 設定ありが先頭、未設定は納入場所コード順で末尾
        Assert.Equal(new[] { "場所3", "場所1", "場所2" }, result.Select(x => x.LocationName));
    }

    [Fact]
    public async Task Search_all_routes_groups_by_route_then_addinfo()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var appDb = NewAppDb(dbName);
        await using var cstmeatDb = NewCstmeatDb(dbName);

        await AddLocationAsync(appDb, 1, "L1", "場所1", "A", "2", "B", "1", "C", "1");
        await AddLocationAsync(appDb, 2, "L2", "場所2", "A", "1", "B", "2", "C", "2");
        // 場所1・場所2 それぞれ朝・昼・夜の3便
        await AddCstmeatAsync(cstmeatDb, 1, "L1", "1");
        await AddCstmeatAsync(cstmeatDb, 2, "L2", "1");
        await AddCstmeatAsync(cstmeatDb, 3, "L1", "2");
        await AddCstmeatAsync(cstmeatDb, 4, "L2", "2");
        await AddCstmeatAsync(cstmeatDb, 5, "L1", "3");
        await AddCstmeatAsync(cstmeatDb, 6, "L2", "3");

        var service = new DeliveryNoteService(cstmeatDb, appDb);
        var result = await service.SearchByEatingDateAsync(EatingDate, "catering");

        // 便(朝→昼→夜)ごとにまとまり、便内は該当 addinfo の配送順
        Assert.Equal(
            new[] { "場所2", "場所1", "場所1", "場所2", "場所1", "場所2" },
            result.Select(x => x.LocationName));
        Assert.Equal(
            new[] { "1", "1", "2", "2", "3", "3" },
            result.Select(x => x.DeliveryRoute));
        Assert.Equal(
            new[] { "出荷朝便", "出荷朝便", "出荷昼便", "出荷昼便", "出荷夜便", "出荷夜便" },
            result.Select(x => x.DeliveryRouteName));
    }
}
