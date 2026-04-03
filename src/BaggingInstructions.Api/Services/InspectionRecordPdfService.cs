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

        var printNow = DateTime.Now;
        var pages = new List<Dictionary<string, string>>();

        var totalPages = Math.Max(1, (lines.Count + RowsPerPage - 1) / RowsPerPage);

        var pageNum = 0;
        for (var off = 0; off < lines.Count; off += RowsPerPage)
        {
            var chunk = lines.Skip(off).Take(RowsPerPage).ToList();
            pageNum++;
            var tags = BuildPageTagValues(chunk);
            JuicePdfService.AddPrintTags(tags, printNow, pageNum, totalPages);
            tags["PRINTPAGE"] = $"{pageNum}/{totalPages}";
            pages.Add(tags);
        }

        return _juicePdf.GeneratePdfMultiPage(rxzTemplatePath, pages, "検品記録簿");
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
