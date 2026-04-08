using BaggingInstructions.Api.DTOs;

namespace BaggingInstructions.Api.Services;

file static class BaggingInputLineLookup
{
    public static bool LinesUseInputOrder(IReadOnlyList<BaggingInputLineDto> lines) =>
        lines.Any(l => l.InputOrder is > 0);

    public static BaggingInputLineDto? FindLine(
        IReadOnlyList<BaggingInputLineDto> lines,
        string citemcd,
        int bomIndexZeroBased)
    {
        if (lines.Count == 0) return null;
        var wantOrder = bomIndexZeroBased + 1;
        if (LinesUseInputOrder(lines))
        {
            return lines.FirstOrDefault(l =>
                string.Equals(l.Citemcd ?? "", citemcd, StringComparison.Ordinal) &&
                l.InputOrder == wantOrder);
        }

        var byCd = lines
            .GroupBy(l => l.Citemcd ?? "", StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Last(), StringComparer.Ordinal);
        byCd.TryGetValue(citemcd, out var line);
        return line;
    }
}

/// <summary>登録済み投入量に基づき、右上表示用の全体量と納品先按分を適用する。</summary>
public static class BaggingSavedInputApplier
{
    public static List<BaggingIngredientRowDto> BuildIngredientDisplayRows(
        IReadOnlyList<MbomDetailDto> mboms,
        IReadOnlyList<decimal> globalTotals,
        BaggingInputPayloadDto? payload)
    {
        var lines = payload?.Lines ?? new List<BaggingInputLineDto>();

        var rows = new List<BaggingIngredientRowDto>();
        for (var j = 0; j < mboms.Count && j < globalTotals.Count; j++)
        {
            var m = mboms[j];
            var cd = m.Citemcd ?? "";
            var line = BaggingInputLineLookup.FindLine(lines, cd, j);
            rows.Add(new BaggingIngredientRowDto
            {
                Citemcd = cd,
                SpecQty = line?.SpecQty,
                TotalQty = globalTotals[j],
                UnitName = m.ChildItem?.Uni?.Uninm
            });
        }

        return rows;
    }

    /// <summary>
    /// 登録投入量でバッチ全体量（右上）は <see cref="ResolveGlobalTotals"/> のまま。
    /// 親品目の規格袋数・端数は施設別 <see cref="BaggingInstructionItemDto.PlannedQuantity"/> で <see cref="RoundingService.RoundUpQuantityWithSeasoning"/>。
    /// <paramref name="globalTotalsForSeasoning"/> を渡すと、子品目（調味液等）は <c>globalTotals[j] × (施設受注 / 受注合計)</c> で按分（仕様②③）。
    /// </summary>
    public static void ApplySavedInputPerFacilityRounding(
        List<BaggingInstructionItemDto> items,
        decimal divisor,
        IReadOnlyList<SeasoningBomRow> seasoningBoms,
        IReadOnlyList<MbomDetailDto> mboms,
        IReadOnlyList<decimal>? globalTotalsForSeasoning = null)
    {
        if (items.Count == 0) return;

        var totalOrder = items.Sum(i => i.PlannedQuantity);
        var useGlobalSplit = globalTotalsForSeasoning is { Count: > 0 }
                             && totalOrder > 0
                             && seasoningBoms.Count > 0;

        foreach (var item in items)
        {
            var (standardCount, irregularCount, seasoningList) = RoundingService.RoundUpQuantityWithSeasoning(
                item.PlannedQuantity, divisor, seasoningBoms, item.Item);
            item.AdjustedQuantity = standardCount + irregularCount;
            item.QuantityForInstruction = item.AdjustedQuantity;
            item.StandardBags = (int)standardCount;
            item.IrregularQuantity = irregularCount;

            if (!useGlobalSplit)
            {
                foreach (var sea in seasoningList)
                {
                    var m = mboms.FirstOrDefault(x =>
                        string.Equals(x.Citemcd, sea.Citemcd, StringComparison.Ordinal));
                    if (m != null)
                        sea.ChildItem = m.ChildItem;
                }

                item.SeasoningAmounts = seasoningList;
                continue;
            }

            var share = item.PlannedQuantity / totalOrder;
            var proportional = new List<SeasoningAmountDto>();
            var n = Math.Min(seasoningBoms.Count, globalTotalsForSeasoning!.Count);
            for (var j = 0; j < n; j++)
            {
                var row = seasoningBoms[j];
                var amt = globalTotalsForSeasoning[j] * share;
                var dto = new SeasoningAmountDto
                {
                    Citemcd = row.ChildItemCd ?? "",
                    Amu = row.Amu,
                    Otp = row.Otp,
                    CalculatedAmount = amt,
                    ChildItem = null
                };
                var m = mboms.FirstOrDefault(x =>
                    string.Equals(x.Citemcd, dto.Citemcd, StringComparison.Ordinal));
                if (m != null)
                    dto.ChildItem = m.ChildItem;
                proportional.Add(dto);
            }

            item.SeasoningAmounts = proportional;
        }
    }

    /// <summary>BOM と受注合計から行ごとの既定総数量を算出（必要量セット）。</summary>
    public static List<decimal> ComputeDefaultGlobalTotals(decimal totalOrderQuantity, IReadOnlyList<MbomDetailDto> mboms)
    {
        var list = new List<decimal>(mboms.Count);
        foreach (var m in mboms)
        {
            var otp = m.Otp ?? 0;
            if (otp > 0)
                list.Add(totalOrderQuantity / otp * (m.Amu ?? 0));
            else
                list.Add(0);
        }

        return list;
    }

    /// <summary>登録ペイロードと既定値でバッチ全体の子品目数量を確定する。</summary>
    public static List<decimal> ResolveGlobalTotals(
        decimal totalOrderQuantity,
        IReadOnlyList<MbomDetailDto> mboms,
        BaggingInputPayloadDto? payload)
    {
        var defaults = ComputeDefaultGlobalTotals(totalOrderQuantity, mboms);
        var lines = payload?.Lines ?? new List<BaggingInputLineDto>();

        var result = new List<decimal>(mboms.Count);
        for (var j = 0; j < mboms.Count; j++)
        {
            var m = mboms[j];
            var cd = m.Citemcd ?? "";
            var line = BaggingInputLineLookup.FindLine(lines, cd, j);
            if (line?.TotalQty.HasValue == true)
                result.Add(line.TotalQty.Value);
            else
                result.Add(j < defaults.Count ? defaults[j] : 0);
        }

        return result;
    }

    /// <summary>合計を維持しつつ整数に按分（最大剰余法）。</summary>
    public static List<int> SplitIntProportional(int total, IReadOnlyList<decimal> weights, decimal weightSum)
    {
        var n = weights.Count;
        var result = new int[n];
        if (n == 0 || weightSum <= 0) return result.ToList();

        var raw = new double[n];
        for (var i = 0; i < n; i++)
            raw[i] = total * (double)(weights[i] / weightSum);

        var floors = new int[n];
        for (var i = 0; i < n; i++)
            floors[i] = (int)Math.Floor(raw[i]);

        var rem = total - floors.Sum();
        var order = Enumerable.Range(0, n)
            .Select(i => (i, frac: raw[i] - Math.Floor(raw[i])))
            .OrderByDescending(x => x.frac)
            .ToList();
        for (var k = 0; k < rem && k < order.Count; k++)
            floors[order[k].i]++;

        return floors.ToList();
    }
}
