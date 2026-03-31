namespace BaggingInstructions.Api.Services;

/// <summary>
/// BOM 所要数: 製造数 × (inputqty / outputqty) × (yieldpercent / 100)。
/// </summary>
public static class PreparationBomQuantity
{
    public static decimal ComputeRequiredQty(decimal parentQty, decimal inputQty, decimal outputQty, decimal yieldPercent)
    {
        if (outputQty == 0) return 0;
        return parentQty * (inputQty / outputQty) * (yieldPercent / 100m);
    }
}
