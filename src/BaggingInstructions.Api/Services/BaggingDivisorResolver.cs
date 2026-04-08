using System.Globalization;
using BaggingInstructions.Api.DTOs;
using BaggingInstructions.Api.Entities;

namespace BaggingInstructions.Api.Services;

/// <summary>
/// 袋詰の規格袋除算に使う規格量（itemadditionalinformation.std1→std2→std3 の順で最初に有効な値、なければ car0）。
/// item.classfication1–3code は分類用で除算には使わない。
/// </summary>
public static class BaggingDivisorResolver
{
    public static decimal ResolveFromItemDetail(ItemDetailDto? item)
    {
        if (item == null) return 1m;
        return ResolveFromStdsAndCar0(item.Std1, item.Std2, item.Std3, item.Kikunip);
    }

    public static decimal ResolveFromAddInfo(ItemAdditionalInformation? addInfo)
    {
        if (addInfo == null) return 1m;
        return ResolveFromStdsAndCar0(addInfo.Std1, addInfo.Std2, addInfo.Std3, addInfo.Car0);
    }

    public static decimal ResolveFromStdsAndCar0(string? std1, string? std2, string? std3, decimal? car0)
    {
        foreach (var std in new[] { std1, std2, std3 })
        {
            if (string.IsNullOrEmpty(std))
                continue;
            if (decimal.TryParse(std.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var s) && s > 0)
                return s;
        }

        if (car0 is decimal c && c > 0)
            return c;
        return 1m;
    }
}
