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

        var mboms = first.Mboms;
        if (mboms.Count == 0)
            return new BaggingCalculateResult { Items = items, IngredientDisplayRows = null };

        var payload = await _baggingInputService.TryGetPayloadAsync(prddt, itemcd, request.JobordPrkeys, ct);

        var q = items.Sum(x => x.PlannedQuantity);
        var globalTotals = BaggingSavedInputApplier.ResolveGlobalTotals(q, mboms, payload);
        var effectiveSpecFillQty = payload?.Lines?.FirstOrDefault(l => l.SpecQty.HasValue && l.SpecQty > 0)?.SpecQty
                                  ?? first.DefaultSpecQty;

        var anyUserEnteredTotalQty = payload?.Lines?.Any(l => l.TotalQty.HasValue) == true;

        if (payload?.Lines is { Count: > 0 })
        {
            BaggingSavedInputApplier.ApplySavedInputPerFacilityRounding(
                items, first.Divisor, first.SeasoningBoms, mboms, globalTotals, effectiveSpecFillQty,
                anyUserEnteredTotalQty);
        }

        // 登録済みペイロードがない場合も右上テーブルを表示（規格数量は品目マスタ STD のデフォルトで補完）
        var effectivePayload = payload?.Lines is { Count: > 0 }
            ? payload
            : new BaggingInputPayloadDto
            {
                Lines = mboms.Select((m, j) => new BaggingInputLineDto
                {
                    Citemcd = m.Citemcd ?? "",
                    InputOrder = j + 1,
                    SpecQty = first.DefaultSpecQty,
                    TotalQty = j < globalTotals.Count ? globalTotals[j] : null
                }).ToList()
            };

        var displayRows = BaggingSavedInputApplier.BuildIngredientDisplayRows(mboms, globalTotals, effectivePayload);
        return new BaggingCalculateResult { Items = items, IngredientDisplayRows = displayRows, EffectiveSpecFillQty = effectiveSpecFillQty };
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

            var result = new BaggingInstructionItemDto
            {
                Shpctrcd = shpctrcd,
                Shpctrnm = shpctrnm,
                Itemcd = first.Itemcd ?? "",
                Itemnm = itemnm,
                Delvedt = first.Delvedt ?? "",
                Shptm = first.Shptm,
                ShptmName = first.ShptmName,
                Addinfo05 = first.Addinfo05,
                EatingTimeLabel = BaggingEatingTimeLabel.MapFromAddinfo05(first.Addinfo05),
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
            };

            if (first.DefaultSpecQty is decimal specQty && specQty > 0)
                BaggingSavedInputApplier.ApplySpecBagCounts(
                    new List<BaggingInstructionItemDto> { result }, specQty, first.Mboms, null);

            results.Add(result);
        }

        // 同一（納入場所・品目・喫食日・便・addinfo05）は合算。得意先コードはキーに含めない（同一施設の二重行防止）。
        return BaggingInstructionItemAggregator.Merge(results);
    }

    public async Task<BaggingRequiredQuantitiesResponseDto> GetRequiredQuantitiesAsync(IReadOnlyList<long> jobordPrkeys, CancellationToken ct = default)
    {
        var rows = await _searchService.SearchDetailByPrkeysAsync(jobordPrkeys.ToList(), ct);
        if (rows.Count == 0)
            return new BaggingRequiredQuantitiesResponseDto();

        var total = rows.Sum(r => r.Jobordqun);
        var mboms = rows[0].Mboms;
        var defaultSpecQty = rows[0].DefaultSpecQty;
        var globals = BaggingSavedInputApplier.ComputeDefaultGlobalTotals(total, mboms);
        var lines = new List<BaggingInputLineDto>();
        for (var j = 0; j < mboms.Count; j++)
        {
            var referenceQty = j < globals.Count ? Math.Ceiling(globals[j]) : (decimal?)null;
            lines.Add(new BaggingInputLineDto
            {
                Citemcd = mboms[j].Citemcd ?? "",
                ChildItemName = mboms[j].ChildItem?.Itemnm,
                InputOrder = j + 1,
                SpecQty = defaultSpecQty,
                TotalQty = referenceQty,
                ReferenceQty = referenceQty
            });
        }

        return new BaggingRequiredQuantitiesResponseDto
        {
            TotalOrderQuantity = total,
            Lines = lines
        };
    }
}
