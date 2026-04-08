using BaggingInstructions.Api.DTOs;

namespace BaggingInstructions.Api.Services;

/// <summary>
/// 受注ベースの切上げ・調味液・（条件付き）Allocation 上書き。
/// <see cref="BaggingCalculatorService.CalculateOrderBasedAsync"/> と同一ロジック（テスト・再利用用）。
/// </summary>
public static class BaggingOrderBasedRounding
{
    public static (decimal AdjustedQuantity, int StandardBags, decimal IrregularQuantity, List<SeasoningAmountDto> SeasoningAmounts)
        ApplyRoundingAndOptionalFloorAllocation(
            decimal totalOrder,
            decimal divisor,
            decimal car0,
            ItemDetailDto? parentItem,
            IReadOnlyList<SeasoningBomRow> seasoningBoms)
    {
        var (standardCount, irregularCount, seasoningList) = RoundingService.RoundUpQuantityWithSeasoning(
            totalOrder, divisor, seasoningBoms, parentItem);
        var adjustedQuantity = standardCount + irregularCount;
        var standardBags = (int)standardCount;
        var irregularQuantity = irregularCount;
        var seasoningAmounts = seasoningList;

        if (BaggingBagCountResolver.UseFloorBagsFromCar0(parentItem))
        {
            var kikunip = car0 > 0 ? car0 : divisor;
            if (kikunip > 0)
            {
                standardBags = AllocationService.CalculateStandardBags(adjustedQuantity, kikunip);
                irregularQuantity = AllocationService.CalculateIrregularQuantity(adjustedQuantity, kikunip);
            }
        }

        return (adjustedQuantity, standardBags, irregularQuantity, seasoningAmounts);
    }
}
