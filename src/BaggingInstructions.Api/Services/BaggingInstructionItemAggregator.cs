using BaggingInstructions.Api.DTOs;

namespace BaggingInstructions.Api.Services;

/// <summary>
/// 袋詰指示書用：同一（納入場所・品目・喫食日・便・addinfo05）の行を合算する。得意先コードはキーに含めない。
/// 同一キーで合算するとき、数量のみ加算し、先に登録された行の addinfo05 / eating_time_label はそのまま維持する。
/// 合算キーは <see cref="BaggingInstructionItemDto.Shpctrnm"/> を優先する。マスタ上で納入場所コードが複数あり名称が同一の場合、
/// コードのみでキー化すると指示書下部に同一施設名が二重行になるため。
/// </summary>
public static class BaggingInstructionItemAggregator
{
    public static string BuildMergeKey(BaggingInstructionItemDto result)
    {
        var norm = (result.Addinfo05 ?? "").Trim();
        var locSegment = !string.IsNullOrWhiteSpace(result.Shpctrnm)
            ? result.Shpctrnm.Trim()
            : (result.Shpctrcd ?? "").Trim();
        return $"{locSegment}_{result.Itemcd}_{result.Delvedt}_{norm}";
    }

    public static List<BaggingInstructionItemDto> Merge(IReadOnlyList<BaggingInstructionItemDto> results)
    {
        var aggregated = new Dictionary<string, BaggingInstructionItemDto>();
        foreach (var result in results)
        {
            var aggKey = BuildMergeKey(result);
            if (!aggregated.TryGetValue(aggKey, out var existing))
            {
                aggregated[aggKey] = result;
                continue;
            }

            existing.PlannedQuantity += result.PlannedQuantity;
            existing.AdjustedQuantity += result.AdjustedQuantity;
            existing.QuantityForInventory += result.QuantityForInventory;
            existing.QuantityForInstruction += result.QuantityForInstruction;
            existing.StandardBags += result.StandardBags;
            existing.IrregularQuantity += result.IrregularQuantity;
            for (var i = 0; i < result.SeasoningAmounts.Count && i < existing.SeasoningAmounts.Count; i++)
                existing.SeasoningAmounts[i].CalculatedAmount += result.SeasoningAmounts[i].CalculatedAmount;
            if (result.SeasoningAmounts.Count > 0 && existing.SeasoningAmounts.Count == 0)
                existing.SeasoningAmounts = result.SeasoningAmounts;
        }

        return aggregated.Values.ToList();
    }
}
