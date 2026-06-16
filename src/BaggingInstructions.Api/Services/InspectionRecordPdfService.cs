using BaggingInstructions.Api.DTOs;

namespace BaggingInstructions.Api.Services;

/// <summary>
/// 検品記録簿.rxz を用いた PDF 生成サービス。
/// テンプレの明細マージ名: ORDERNO / ITEMCD / ITEMNM / STANDARD（規格）/ QUANTITY00-16（数量）・QUANTITY17-33（単位）ほか。
/// </summary>
public sealed class InspectionRecordPdfService
{
    /// <summary>rxz 明細行インデックス 00〜16。</summary>
    private const int TemplateDataRowCount = 17;

    private const int RowsPerPage = 12;

    /// <summary>同一行の単位列は QUANTITY(row+17)（テンプレ設計）。</summary>
    private const int QuantityUnitIndexOffset = 17;

    /// <summary>QUANTITY00〜33。</summary>
    private const int QuantityMergeFieldCount = 34;

    private readonly JuicePdfService _juicePdf;

    public InspectionRecordPdfService(JuicePdfService juicePdf)
    {
        _juicePdf = juicePdf;
    }

    public byte[] GeneratePdf(string rxzTemplatePath, IReadOnlyList<InspectionRecordPdfLineModel> lines)
    {
        if (lines == null || lines.Count == 0)
            return Array.Empty<byte>();

        var pageChunks = SplitIntoPages(lines);
        var printNow = DateTime.Now;
        var pages = new List<Dictionary<string, string>>();
        var totalPages = pageChunks.Count;

        for (var i = 0; i < pageChunks.Count; i++)
        {
            var tags = BuildPageTagValues(pageChunks[i]);
            JuicePdfService.AddPrintTags(tags, printNow, i + 1, totalPages);
            tags["PRINTPAGE"] = $"{i + 1}/{totalPages}";
            pages.Add(tags);
        }

        return _juicePdf.GeneratePdfMultiPage(rxzTemplatePath, pages, "検品記録簿");
    }

    /// <summary>
    /// 仕入先名 → 仕入先コード → 注番 でソート後、
    /// 仕入先コードが変わるか 12 行超で改ページする。
    /// </summary>
    internal static List<List<InspectionRecordPdfLineModel>> SplitIntoPages(IReadOnlyList<InspectionRecordPdfLineModel> lines)
    {
        var sorted = lines
            .OrderBy(l => l.SupplierName ?? "", StringComparer.Ordinal)
            .ThenBy(l => l.SupplierCode ?? "", StringComparer.Ordinal)
            .ThenBy(l => l.OrderNo ?? "", StringComparer.Ordinal)
            .ToList();

        var pages = new List<List<InspectionRecordPdfLineModel>>();
        List<InspectionRecordPdfLineModel>? current = null;
        string? supplierOnPage = null;

        foreach (var line in sorted)
        {
            var supplier = line.SupplierCode ?? "";
            var needNewPage = current == null
                              || current.Count >= RowsPerPage
                              || supplierOnPage != supplier;

            if (needNewPage)
            {
                current = new List<InspectionRecordPdfLineModel>();
                pages.Add(current);
                supplierOnPage = supplier;
            }

            current!.Add(line);
        }

        return pages;
    }

    internal static Dictionary<string, string> BuildPageTagValues(IReadOnlyList<InspectionRecordPdfLineModel> chunk)
    {
        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        tags["DELVDATE"] = chunk.FirstOrDefault()?.DeliveryDateDisplay ?? string.Empty;

        for (var i = 0; i < TemplateDataRowCount; i++)
        {
            var nn = i.ToString("D2");
            tags[$"LOCATIONNM{nn}"] = "";
            tags[$"ORDERNO{nn}"] = "";
            tags[$"ITEMCD{nn}"] = "";
            tags[$"ITEMNM{nn}"] = "";
            tags[$"STANDARD{nn}"] = "";
            tags[$"CARE{nn}"] = "";
            tags[$"SAVE{nn}"] = "";
            tags[$"DELVTIME{nn}"] = "";
            tags[$"TESTTEMP{nn}"] = "";
            tags[$"BBD{nn}"] = "";
            tags[$"FRESHNESS{nn}"] = "";
            tags[$"EXT{nn}"] = "";
        }

        for (var q = 0; q < QuantityMergeFieldCount; q++)
            tags[$"QUANTITY{q:D2}"] = "";

        for (var i = 0; i < chunk.Count && i < RowsPerPage; i++)
        {
            var r = chunk[i];
            var nn = i.ToString("D2");
            var unitFieldIndex = i + QuantityUnitIndexOffset;
            var unitNn = unitFieldIndex.ToString("D2");

            tags[$"LOCATIONNM{nn}"] = r.SupplierName ?? string.Empty;
            tags[$"ORDERNO{nn}"] = r.OrderNo ?? string.Empty;
            tags[$"ITEMCD{nn}"] = r.ItemCode ?? string.Empty;
            tags[$"ITEMNM{nn}"] = r.ItemName ?? string.Empty;
            tags[$"STANDARD{nn}"] = r.Spec ?? string.Empty;
            tags[$"QUANTITY{nn}"] = r.QuantityDisplay ?? string.Empty;
            tags[$"QUANTITY{unitNn}"] = r.UnitName ?? string.Empty;

            tags[$"CARE{nn}"] = r.DeviationHandling ?? string.Empty;
            tags[$"SAVE{nn}"] = r.StorageLocation ?? string.Empty;
            tags[$"DELVTIME{nn}"] = r.DeliveryTime ?? string.Empty;
            tags[$"TESTTEMP{nn}"] = r.TemperatureCheck ?? string.Empty;
            tags[$"BBD{nn}"] = r.BestBefore ?? string.Empty;
            tags[$"FRESHNESS{nn}"] = r.FreshnessGrade ?? string.Empty;
            tags[$"EXT{nn}"] = r.ExternalAppearance ?? string.Empty;
        }

        return tags;
    }
}
