using System.Globalization;
using BaggingInstructions.Api.QueryResults;

namespace BaggingInstructions.Api.Services;

/// <summary>現品票（調理）1枚.rxz：1ページ1ラベル（最終完成品＋BOM子品目1件）。</summary>
public sealed class ProductLabelPdfService
{
    public const string TemplateFileName = "現品票（調理）1枚.rxz";
    public const string BaggingTemplateFileName = "袋詰現品票1枚.rxz";

    private static readonly IReadOnlyDictionary<string, int> AlignmentOverrides =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["DATE01"]        = 1, // 左詰め
            ["ITEMNO01"]      = 1,
            ["ITEMNM01"]      = 1,
            ["CHILDITEMNO01"] = 1,
            ["CHILDITEMNM01"] = 1,
            ["QUANTITY01"]    = 2, // 右詰め
            ["UNIT01"]        = 1,
            ["BBDT01"]        = 1,
        };

    private readonly SearchService _searchService;
    private readonly JuicePdfService _juicePdf;

    public ProductLabelPdfService(SearchService searchService, JuicePdfService juicePdf)
    {
        _searchService = searchService;
        _juicePdf = juicePdf;
    }

    /// <param name="labelCount">1ラベルあたりの印刷枚数（デフォルト1）。perRowCounts で上書き可能。</param>
    /// <param name="instructionType">指示書種別（"cut"/"seasoning"/"cooking"/null）。指定時はBOM再帰探索で子品目を抽出。</param>
    /// <param name="perRowCounts">オーダID別の印刷枚数上書き。キーは ordertableid の文字列。</param>
    public async Task<byte[]> GeneratePdfAsync(
        string rxzTemplatePath,
        IReadOnlyList<long> orderTableIds,
        int labelCount = 1,
        string? instructionType = null,
        IReadOnlyDictionary<string, int>? perRowCounts = null,
        CancellationToken ct = default)
    {
        var idList = orderTableIds.Where(id => id > 0).Distinct().ToList();
        if (idList.Count == 0)
            return Array.Empty<byte>();

        List<ProductLabelOrderSqlRow> rows;
        if (!string.IsNullOrWhiteSpace(instructionType))
            rows = await _searchService.LoadProductLabelOrdersByBomTraversalAsync(idList, instructionType, ct);
        else
            rows = await _searchService.LoadProductLabelOrdersByIdsAsync(idList, ct);

        if (rows.Count == 0)
            return Array.Empty<byte>();

        var defaultCount = Math.Max(1, labelCount);
        var pages = new List<Dictionary<string, string>>(rows.Count * defaultCount);
        foreach (var row in rows)
        {
            var tags = BuildPageTags(row);
            var rowCount = defaultCount;
            if (perRowCounts != null && perRowCounts.TryGetValue(row.OrderTableId.ToString(), out var rc))
                rowCount = Math.Max(1, rc);
            for (var i = 0; i < rowCount; i++)
                pages.Add(tags);
        }

        return _juicePdf.GeneratePdfMultiPage(rxzTemplatePath, pages, "現品票（調理）1枚", AlignmentOverrides);
    }

    private static Dictionary<string, string> BuildPageTags(ProductLabelOrderSqlRow row)
    {
        var expiryDate = row.ReleaseDate.HasValue
            ? row.ReleaseDate.Value
                  .AddDays(row.ShelflifeDays > 0 ? row.ShelflifeDays : LabelGeneratorService.DefaultDaysAfter)
                  .ToString("yyyy/MM/dd", CultureInfo.InvariantCulture)
            : "";

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["DATE01"]        = row.ReleaseDate?.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture) ?? "",
            ["ITEMNO01"]      = row.ParentItemCode,
            ["ITEMNM01"]      = row.ParentItemName,
            ["CHILDITEMNO01"] = row.ChildItemCode,
            ["CHILDITEMNM01"] = row.ChildItemName,
            ["QUANTITY01"]    = FormatQty(row.ChildQty),
            ["UNIT01"]        = row.ChildUnitName,
            ["BBDT01"]        = expiryDate,
        };
    }

    private static string FormatQty(decimal q) => ReportQuantityFormatter.FormatCeilingQuantity(q);
}
