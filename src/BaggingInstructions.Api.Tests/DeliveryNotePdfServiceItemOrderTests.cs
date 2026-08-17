using BaggingInstructions.Api.Core;
using BaggingInstructions.Api.Entities;
using BaggingInstructions.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace BaggingInstructions.Api.Tests;

/// <summary>
/// 納品書の明細行の並び順：区分（info06）1:通常品 → 2:検食 → 3:検体、同一区分内は明細名の昇順。
/// </summary>
public class DeliveryNotePdfServiceItemOrderTests
{
    private const string EatingDate = "20260810";
    private const string CustomerCode = "200";
    private const string LocationCode = "L1";

    private static AppDbContext NewAppDb(string name) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase($"{name}-app").Options);

    private static CstmeatDbContext NewCstmeatDb(string name) =>
        new(new DbContextOptionsBuilder<CstmeatDbContext>().UseInMemoryDatabase($"{name}-cstmeat").Options);

    private static Cstmeat NewCstmeat(int id, string shokushuCode, string eattimeCd, string info06) => new()
    {
        CstmeatId = id,
        Info01 = CustomerCode,
        Info02 = LocationCode,
        Info04 = eattimeCd,
        Info05 = shokushuCode,
        Info06 = info06,
        Info07 = "10",
        Info18 = EatingDate,
        Info19 = "1",
        Info22 = "1"   // CstmeatDbContext のグローバルフィルタ（有効データ）
    };

    [Fact]
    public void Items_are_ordered_by_info06_then_name()
    {
        var dbName = Guid.NewGuid().ToString();
        using var appDb = NewAppDb(dbName);
        using var cstmeatDb = NewCstmeatDb(dbName);

        cstmeatDb.Mshokushus.AddRange(
            new Mshokushu { Id = 1, ShokushuCode = "S1", ShokushuName = "A食" },
            new Mshokushu { Id = 2, ShokushuCode = "S2", ShokushuName = "B食" });
        cstmeatDb.Eattimes.Add(new Eattime { Eattimecd = "1", Eattimename = "朝食" });
        cstmeatDb.Cstmeats.AddRange(
            NewCstmeat(1, "S2", "1", "1"),   // B食:朝食:通常品
            NewCstmeat(2, "S1", "1", "2"),   // A食:朝食:検食
            NewCstmeat(3, "S1", "1", "3"),   // A食:朝食:検体
            NewCstmeat(4, "S1", "1", "1"));  // A食:朝食:通常品
        cstmeatDb.SaveChanges();

        var service = new DeliveryNotePdfService(cstmeatDb, appDb, new JuicePdfService());
        var pages = service.BuildTagValuesPagesForOne(EatingDate, LocationCode, CustomerCode, "1");

        var page = Assert.Single(pages);
        Assert.Equal(
            new[] { "A食:朝食:通常品", "B食:朝食:通常品", "A食:朝食:検食", "A食:朝食:検体" },
            new[] { page["ITEMNM_0_00"], page["ITEMNM_0_01"], page["ITEMNM_0_02"], page["ITEMNM_0_03"] });
    }

    [Fact]
    public void Items_without_info06_are_placed_last()
    {
        var dbName = Guid.NewGuid().ToString();
        using var appDb = NewAppDb(dbName);
        using var cstmeatDb = NewCstmeatDb(dbName);

        cstmeatDb.Mshokushus.Add(new Mshokushu { Id = 1, ShokushuCode = "S1", ShokushuName = "A食" });
        cstmeatDb.Eattimes.Add(new Eattime { Eattimecd = "1", Eattimename = "朝食" });
        cstmeatDb.Cstmeats.AddRange(
            NewCstmeat(1, "S1", "1", ""),    // 区分なし
            NewCstmeat(2, "S1", "1", "3"),   // A食:朝食:検体
            NewCstmeat(3, "S1", "1", "1"));  // A食:朝食:通常品
        cstmeatDb.SaveChanges();

        var service = new DeliveryNotePdfService(cstmeatDb, appDb, new JuicePdfService());
        var pages = service.BuildTagValuesPagesForOne(EatingDate, LocationCode, CustomerCode, "1");

        var page = Assert.Single(pages);
        Assert.Equal(
            new[] { "A食:朝食:通常品", "A食:朝食:検体", "A食:朝食" },
            new[] { page["ITEMNM_0_00"], page["ITEMNM_0_01"], page["ITEMNM_0_02"] });
        Assert.Equal("", page["ITEMNM_0_03"]);
    }
}
