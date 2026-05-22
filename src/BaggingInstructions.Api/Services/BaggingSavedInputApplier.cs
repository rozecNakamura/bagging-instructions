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
    /// <paramref name="specQty"/> 指定時は、先頭レシピ子品目の単位に応じて STD 規格量で袋数を上書きする。
    /// <paramref name="globalTotalsForSeasoning"/> を渡すと、子品目（調味液等）は <c>globalTotals[j] × (施設受注 / 受注合計)</c> で按分（仕様②③）。
    /// </summary>
    public static void ApplySavedInputPerFacilityRounding(
        List<BaggingInstructionItemDto> items,
        decimal divisor,
        IReadOnlyList<SeasoningBomRow> seasoningBoms,
        IReadOnlyList<MbomDetailDto> mboms,
        IReadOnlyList<decimal>? globalTotalsForSeasoning = null,
        decimal? specQty = null,
        bool useIngredientAllocation = false)
    {
        if (items.Count == 0) return;

        var totalOrder = items.Sum(i => i.PlannedQuantity);
        var useGlobalSplit = globalTotalsForSeasoning is { Count: > 0 }
                             && totalOrder > 0
                             && seasoningBoms.Count > 0;
        // g/kg品目+総数量入力時: seasoningBomsを使わず mboms×globalTotals×share で按分
        var useGKgIngredientPath = useIngredientAllocation
                                   && globalTotalsForSeasoning is { Count: > 0 }
                                   && totalOrder > 0
                                   && mboms.Count > 0;

        foreach (var item in items)
        {
            var (standardCount, irregularCount, seasoningList) = RoundingService.RoundUpQuantityWithSeasoning(
                item.PlannedQuantity, divisor, seasoningBoms, item.Item);
            item.AdjustedQuantity = standardCount + irregularCount;
            item.QuantityForInstruction = item.AdjustedQuantity;
            item.StandardBags = (int)standardCount;
            item.IrregularQuantity = irregularCount;

            if (useGKgIngredientPath)
            {
                var share = item.PlannedQuantity / totalOrder;
                var proportional = new List<SeasoningAmountDto>();
                var n = Math.Min(mboms.Count, globalTotalsForSeasoning!.Count);
                for (var j = 0; j < n; j++)
                {
                    var m = mboms[j];
                    proportional.Add(new SeasoningAmountDto
                    {
                        Citemcd = m.Citemcd ?? "",
                        Amu = m.Amu ?? 0,
                        Otp = m.Otp ?? 0,
                        CalculatedAmount = globalTotalsForSeasoning[j] * share,
                        ChildItem = m.ChildItem
                    });
                }
                item.SeasoningAmounts = proportional;
                continue;
            }

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

            var share2 = item.PlannedQuantity / totalOrder;
            var proportional2 = new List<SeasoningAmountDto>();
            var n2 = Math.Min(seasoningBoms.Count, globalTotalsForSeasoning!.Count);
            for (var j = 0; j < n2; j++)
            {
                var row = seasoningBoms[j];
                var amt = globalTotalsForSeasoning[j] * share2;
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
                proportional2.Add(dto);
            }

            item.SeasoningAmounts = proportional2;
        }

        if (specQty is decimal sq && sq > 0)
            ApplySpecBagCounts(items, sq, mboms, globalTotalsForSeasoning, useIngredientAllocation);
    }

    /// <summary>
    /// 規格数量に基づき、各施設の規格袋数と端数を計算して上書きする。
    /// <paramref name="useIngredientAllocation"/> が true かつ先頭子品目が g/kg 単位の場合、
    /// ユーザーが入力した総数量から有効製品量（childAllocated ÷ amu/otp）を逆算して端数を求める。
    /// false の場合は常に PlannedQuantity を基準にする。
    /// </summary>
    public static void ApplySpecBagCounts(
        List<BaggingInstructionItemDto> items,
        decimal specQty,
        IReadOnlyList<MbomDetailDto>? mboms = null,
        IReadOnlyList<decimal>? globalTotalsForSeasoning = null,
        bool useIngredientAllocation = false)
    {
        if (items.Count == 0 || specQty <= 0) return;

        var totalOrder = items.Sum(i => i.PlannedQuantity);
        var firstChildIndex = mboms != null ? FirstRecipeChildIndex(mboms) : -1;
        var firstUnitName = (firstChildIndex >= 0 && mboms != null) ? ChildUnitName(mboms[firstChildIndex]) : null;

        // g/kg子品目 + TotalQty入力時: 全品目の入力総数量合計を受注比で直接按分（specQtyと同単位のため変換不要）
        var useDirectChildAllocation = useIngredientAllocation
            && firstChildIndex >= 0
            && mboms != null
            && IsGramOrKg(ChildUnitName(mboms[firstChildIndex]))
            && globalTotalsForSeasoning is { Count: > 0 }
            && totalOrder > 0;

        foreach (var item in items)
        {
            if (useDirectChildAllocation)
            {
                // 按分量を規格数量で割り、余りをそのまま端数とする（切り上げなし→合計Aと一致）
                var share = totalOrder > 0 ? item.PlannedQuantity / totalOrder : 0m;
                var baseQty = globalTotalsForSeasoning!.Sum() * share;
                var standardBags = (int)Math.Floor(baseQty / specQty);
                var rem = baseQty % specQty;
                const decimal eps = 0.0000000001m;
                var irregularQuantity = rem > eps ? rem : 0m;
                item.StandardBags = standardBags;
                item.IrregularQuantity = irregularQuantity;
                item.AdjustedQuantity = standardBags + (irregularQuantity > 0 ? 1 : 0);
                item.QuantityForInstruction = item.AdjustedQuantity;
            }
            else
            {
                var (standardBags, irregularQuantity) = CalculateBagsAndRoundedRemainder(item.PlannedQuantity, specQty);
                item.StandardBags = standardBags;
                item.IrregularQuantity = irregularQuantity;
                item.AdjustedQuantity = standardBags + irregularQuantity;
                item.QuantityForInstruction = item.AdjustedQuantity;
            }
        }
    }

    private static (int StandardBags, decimal IrregularQuantity) CalculateBagsAndRoundedRemainder(decimal quantity, decimal specQty)
    {
        if (specQty <= 0) return (0, quantity);
        var standardBags = (int)Math.Floor(quantity / specQty);
        var remainder = quantity % specQty;
        const decimal epsilon = 0.0000000001m;
        var roundedRemainder = remainder > epsilon ? Math.Ceiling(remainder) : 0m;
        return (standardBags, roundedRemainder);
    }

    private static int FirstRecipeChildIndex(IReadOnlyList<MbomDetailDto> mboms)
    {
        if (mboms.Count == 0) return -1;
        var zeroIndex = Enumerable.Range(0, mboms.Count)
            .FirstOrDefault(i => mboms[i].Proutno == 0);
        if (zeroIndex >= 0 && mboms[zeroIndex].Proutno == 0)
            return zeroIndex;

        return Enumerable.Range(0, mboms.Count)
            .OrderBy(i => mboms[i].Proutno)
            .First();
    }

    private static string? ChildUnitName(MbomDetailDto mbom) =>
        mbom.ChildItem?.Uni?.Uninm ?? mbom.ChildItem?.Uni?.Uniinfnm;

    private static bool IsGramOrKg(string? unitName)
    {
        if (string.IsNullOrWhiteSpace(unitName)) return false;
        var normalized = unitName.Trim().ToLowerInvariant();
        return normalized is "g" or "ｇ" or "kg" or "ｋｇ" or "㎏"
               || normalized.Contains("グラム", StringComparison.Ordinal)
               || normalized.Contains("キログラム", StringComparison.Ordinal);
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
