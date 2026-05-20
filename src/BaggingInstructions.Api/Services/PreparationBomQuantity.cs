namespace BaggingInstructions.Api.Services;

/// <summary>
/// BOM 所要数: 製造数 × (inputqty / outputqty) ÷ (yieldpercent / 100)。
/// 歩留まりは損失率のため除算（例: 75% = 実際に必要な原料 ÷ 0.75）。
/// yieldpercent が 0 以下の場合は歩留まり補正なし（100% 扱い）。
/// </summary>
public static class PreparationBomQuantity
{
    public static decimal ComputeRequiredQty(decimal parentQty, decimal inputQty, decimal outputQty, decimal yieldPercent)
    {
        if (outputQty == 0) return 0;
        var yield = yieldPercent > 0 ? yieldPercent / 100m : 1m;
        return parentQty * (inputQty / outputQty) / yield;
    }
}
