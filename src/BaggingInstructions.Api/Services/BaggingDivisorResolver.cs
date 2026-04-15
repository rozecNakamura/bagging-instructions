using BaggingInstructions.Api.DTOs;
using BaggingInstructions.Api.Entities;

namespace BaggingInstructions.Api.Services;

/// <summary>
/// 袋詰の規格袋除算に使う規格量（itemadditionalinformation.car1→car2→car3 の順で最初に有効な値、なければ car0）。
/// item.classfication1–3code は分類用で除算には使わない。
/// </summary>
public static class BaggingDivisorResolver
{
    public static decimal ResolveFromItemDetail(ItemDetailDto? item)
    {
        if (item == null) return 1m;
        return ResolveFromCar123AndCar0(item.Car1, item.Car2, item.Car3, item.Kikunip);
    }

    public static decimal ResolveFromAddInfo(ItemAdditionalInformation? addInfo)
    {
        if (addInfo == null) return 1m;
        return ResolveFromCar123AndCar0(addInfo.Car1, addInfo.Car2, addInfo.Car3, addInfo.Car0);
    }

    public static decimal ResolveFromCar123AndCar0(decimal? car1, decimal? car2, decimal? car3, decimal? car0)
    {
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
