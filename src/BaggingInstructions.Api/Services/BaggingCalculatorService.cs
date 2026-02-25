using BaggingInstructions.Api.DTOs;

namespace BaggingInstructions.Api.Services;

public class BaggingCalculatorService
{
    private readonly SearchService _searchService;
    private readonly StockService _stockService;

    private const bool EnableRounding = true;
    private const bool EnableAllocation = false;
    private const bool EnableAggregation = false;

    public BaggingCalculatorService(SearchService searchService, StockService stockService)
    {
        _searchService = searchService;
        _stockService = stockService;
    }

    public async Task<List<BaggingInstructionItemDto>> CalculateAsync(IReadOnlyList<long> jobordPrkeys, CancellationToken ct = default)
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

            var currentStock = await _stockService.GetItemStockByItemIdAsync(first.ItemId, ct);

            decimal adjustedQuantity;
            int standardBags;
            decimal irregularQuantity;
            List<SeasoningAmountDto> seasoningAmounts;

            if (EnableRounding)
            {
                var (standardCount, irregularCount, seasoningList) = RoundingService.RoundUpQuantityWithSeasoning(
                    totalOrder, first.Divisor, first.SeasoningBoms);
                adjustedQuantity = standardCount + irregularCount;
                standardBags = (int)standardCount;
                irregularQuantity = irregularCount;
                seasoningAmounts = seasoningList;
            }
            else
            {
                adjustedQuantity = totalOrder;
                standardBags = 0;
                irregularQuantity = 0;
                seasoningAmounts = new List<SeasoningAmountDto>();
            }

            if (EnableAllocation)
            {
                var kikunip = first.Car0;
                standardBags = AllocationService.CalculateStandardBags(adjustedQuantity, kikunip);
                irregularQuantity = AllocationService.CalculateIrregularQuantity(adjustedQuantity, kikunip);
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
            var aggKey = $"{cuscd}_{result.Shpctrcd}_{result.Itemcd}";
            if (!aggregated.TryGetValue(aggKey, out var existing))
            {
                aggregated[aggKey] = result;
                continue;
            }
            existing.PlannedQuantity += result.PlannedQuantity;
            existing.AdjustedQuantity += result.AdjustedQuantity;
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
