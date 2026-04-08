using BaggingInstructions.Api.DTOs;

namespace BaggingInstructions.Api.Services;

public class BaggingCalculatorService
{
    private readonly SearchService _searchService;
    private readonly StockService _stockService;
    private readonly BaggingInputService _baggingInputService;

    private const bool EnableRounding = true;
    private const bool EnableAggregation = true;

    public BaggingCalculatorService(
        SearchService searchService,
        StockService stockService,
        BaggingInputService baggingInputService)
    {
        _searchService = searchService;
        _stockService = stockService;
        _baggingInputService = baggingInputService;
    }

    public async Task<BaggingCalculateResult> CalculateFullAsync(CalculateRequestDto request, CancellationToken ct = default)
    {
        var items = await CalculateOrderBasedAsync(request.JobordPrkeys, ct);

        if (!request.UseSavedInput || request.JobordPrkeys.Count == 0)
            return new BaggingCalculateResult { Items = items, IngredientDisplayRows = null };

        var rows = await _searchService.SearchDetailByPrkeysAsync(request.JobordPrkeys.ToList(), ct);
        if (rows.Count == 0)
            return new BaggingCalculateResult { Items = items, IngredientDisplayRows = null };

        var first = rows[0];
        var prddt = first.Prddt ?? "";
        var itemcd = first.Itemcd ?? "";
        if (string.IsNullOrEmpty(prddt) || string.IsNullOrEmpty(itemcd))
            return new BaggingCalculateResult { Items = items, IngredientDisplayRows = null };

        var payload = await _baggingInputService.TryGetPayloadAsync(prddt, itemcd, request.JobordPrkeys, ct);
        if (payload?.Lines == null || payload.Lines.Count == 0)
            return new BaggingCalculateResult { Items = items, IngredientDisplayRows = null };

        var mboms = first.Mboms;
        if (mboms.Count == 0)
            return new BaggingCalculateResult { Items = items, IngredientDisplayRows = null };

        var q = items.Sum(x => x.PlannedQuantity);
        var globalTotals = BaggingSavedInputApplier.ResolveGlobalTotals(q, mboms, payload);

        BaggingSavedInputApplier.ApplySavedInputPerFacilityRounding(
            items, first.Divisor, first.SeasoningBoms, mboms, globalTotals);
        var displayRows = BaggingSavedInputApplier.BuildIngredientDisplayRows(mboms, globalTotals, payload);

        return new BaggingCalculateResult { Items = items, IngredientDisplayRows = displayRows };
    }

    /// <summary>受注ベースの袋詰計算（従来ロジック）。</summary>
    public async Task<List<BaggingInstructionItemDto>> CalculateOrderBasedAsync(IReadOnlyList<long> jobordPrkeys, CancellationToken ct = default)
    {
        var rows = await _searchService.SearchDetailByPrkeysAsync(jobordPrkeys.ToList(), ct);
        if (rows.Count == 0)
            return new List<BaggingInstructionItemDto>();

        var grouped = EnableAggregation
            ? AggregationRuleService.ApplyAggregationRule(rows)
            : rows.ToDictionary(r => r.Prkey.ToString(), r => new List<BaggingDetailRow> { r });

        var results = new List<BaggingInstructionItemDto>();

        foreach (var (key, groupRows) in grouped)
        {
            var first = groupRows[0];
            var totalOrder = groupRows.Sum(r => r.Jobordqun);

            var currentStock = await _stockService.GetItemStockByItemCodeAsync(first.Itemcd, ct);

            decimal adjustedQuantity = totalOrder;
            var standardBags = 0;
            decimal irregularQuantity = 0;
            List<SeasoningAmountDto> seasoningAmounts = new List<SeasoningAmountDto>();

            if (EnableRounding)
            {
                (adjustedQuantity, standardBags, irregularQuantity, seasoningAmounts) =
                    BaggingOrderBasedRounding.ApplyRoundingAndOptionalFloorAllocation(
                        totalOrder, first.Divisor, first.Car0, first.Item, first.SeasoningBoms);
            }

            string shpctrnm;
            string? shpctrcd;
            if (EnableAggregation && key.Contains("_CATERING_", StringComparison.Ordinal))
            {
                shpctrnm = "ケータリング";
                shpctrcd = null;
            }
            else
            {
                shpctrnm = first.Shpctrnm ?? first.Shpctrcd ?? "不明";
                shpctrcd = first.Shpctrcd;
            }

            var itemnm = first.Jobordmernm ?? first.Itemcd ?? "";

            results.Add(new BaggingInstructionItemDto
            {
                Shpctrcd = shpctrcd,
                Shpctrnm = shpctrnm,
                Itemcd = first.Itemcd ?? "",
                Itemnm = itemnm,
                Delvedt = first.Delvedt ?? "",
                Shptm = first.Shptm,
                PlannedQuantity = totalOrder,
                AdjustedQuantity = adjustedQuantity,
                QuantityForInventory = totalOrder,
                QuantityForInstruction = adjustedQuantity,
                StandardBags = standardBags,
                IrregularQuantity = irregularQuantity,
                Prddt = first.Prddt,
                CurrentStock = currentStock,
                SeasoningAmounts = seasoningAmounts,
                Item = first.Item,
                Shpctr = first.Shpctr,
                Mboms = first.Mboms,
                Cusmcd = first.Cusmcd,
                Jobordno = first.Jobordno,
                Jobordmernm = first.Jobordmernm
            });
        }

        var aggregated = new Dictionary<string, BaggingInstructionItemDto>();
        foreach (var result in results)
        {
            var cuscd = result.Shpctr?.Cuscd ?? "";
            var aggKey =
                $"{cuscd}_{result.Shpctrcd ?? ""}_{result.Itemcd}_{result.Delvedt}_{result.Shptm ?? ""}";
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

    public async Task<BaggingRequiredQuantitiesResponseDto> GetRequiredQuantitiesAsync(IReadOnlyList<long> jobordPrkeys, CancellationToken ct = default)
    {
        var rows = await _searchService.SearchDetailByPrkeysAsync(jobordPrkeys.ToList(), ct);
        if (rows.Count == 0)
            return new BaggingRequiredQuantitiesResponseDto();

        var total = rows.Sum(r => r.Jobordqun);
        var mboms = rows[0].Mboms;
        var globals = BaggingSavedInputApplier.ComputeDefaultGlobalTotals(total, mboms);
        var lines = new List<BaggingInputLineDto>();
        for (var j = 0; j < mboms.Count; j++)
        {
            lines.Add(new BaggingInputLineDto
            {
                Citemcd = mboms[j].Citemcd ?? "",
                InputOrder = j + 1,
                SpecQty = null,
                TotalQty = j < globals.Count ? globals[j] : null
            });
        }

        return new BaggingRequiredQuantitiesResponseDto
        {
            TotalOrderQuantity = total,
            Lines = lines
        };
    }
}
