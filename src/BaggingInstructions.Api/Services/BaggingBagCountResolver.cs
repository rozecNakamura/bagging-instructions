using BaggingInstructions.Api.DTOs;

namespace BaggingInstructions.Api.Services;

/// <summary>
/// 袋詰の規格袋数・端数の決め方。
/// 個数単位の完成品は <see cref="RoundingService.RoundUpQuantityWithSeasoning"/>（切上げ端数）。
/// 液体は rounding の液体パス。それ以外の完成品は <see cref="AllocationService"/>（car0 で floor 袋数・mod 端数）。
/// </summary>
public static class BaggingBagCountResolver
{
    /// <summary>rounding 後の adjustedQuantity に対し、floor 袋数・剰余端数で上書きするか。</summary>
    public static bool UseFloorBagsFromCar0(ItemDetailDto? item) =>
        item != null
        && !ItemCodeKind.IsLiquid(item.Itemcd)
        && !RoundingService.FinishedGoodUsesCountRounding(item);
}
