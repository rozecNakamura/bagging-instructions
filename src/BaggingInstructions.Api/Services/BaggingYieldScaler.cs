using BaggingInstructions.Api.DTOs;

namespace BaggingInstructions.Api.Services;

/// <summary>登録された親出来高で、施設別の <see cref="BaggingInstructionItemDto.PlannedQuantity"/> を受注比按分する。</summary>
public static class BaggingYieldScaler
{
    public static void ApplyParentYieldToPlannedQuantities(
        List<BaggingInstructionItemDto> items,
        decimal? parentYieldQuantity)
    {
        if (parentYieldQuantity is not decimal py || py <= 0 || items.Count == 0)
            return;

        var totalOrder = items.Sum(x => x.PlannedQuantity);
        if (totalOrder <= 0)
            return;

        foreach (var item in items)
            item.PlannedQuantity = py * (item.PlannedQuantity / totalOrder);
    }
}
