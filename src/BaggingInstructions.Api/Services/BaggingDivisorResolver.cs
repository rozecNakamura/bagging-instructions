using System.Globalization;
using BaggingInstructions.Api.DTOs;
using BaggingInstructions.Api.Entities;

namespace BaggingInstructions.Api.Services;

/// <summary>
/// 袋詰の規格袋除算に使う規格量（std 優先、なければ car1→car2→car3、なければ car0）。
/// item.classfication1–3code は分類用で除算には使わない。
/// </summary>
public static class BaggingDivisorResolver
{
    public static decimal ResolveFromItemDetail(ItemDetailDto? item)
    {
        if (item == null) return 1m;
        return ResolveChain(item.Car1, item.Car2, item.Car3, item.Std, item.Kikunip);
    }

    public static decimal ResolveFromAddInfo(ItemAdditionalInformation? addInfo)
    {
        if (addInfo == null) return 1m;
        return ResolveChain(addInfo.Car1, addInfo.Car2, addInfo.Car3, addInfo.Std, addInfo.Car0);
    }

    public static decimal ResolveFromCar123AndCar0(decimal? car1, decimal? car2, decimal? car3, decimal? car0)
    {
        return ResolveChain(car1, car2, car3, null, car0);
    }

    /// <summary>規格テキストを正の小数に変換。変換できない場合は null。</summary>
    public static decimal? ParseStdToDecimal(string? std)
    {
        if (string.IsNullOrWhiteSpace(std)) return null;
        return decimal.TryParse(std.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d) && d > 0
            ? d : null;
    }

    private static decimal ResolveChain(decimal? car1, decimal? car2, decimal? car3, string? std, decimal? car0)
    {
        if (ParseStdToDecimal(std) is decimal sv)
            return sv;
        foreach (var c in new[] { car1, car2, car3 })
        {
            if (c is decimal d && d > 0)
                return d;
        }
        if (car0 is decimal c0 && c0 > 0)
            return c0;
        return 1m;
    }
}
