using System.Globalization;
using BaggingInstructions.Api.DTOs;

namespace BaggingInstructions.Api.Services;

/// <summary>
/// 検収の記録簿.rxz を用いた PDF 生成。
/// ページング: 施設名（納入場所名称）・出荷日・納品日の組が変わるごとに改ページし、同一組内は最大 14 行で折り返す（喫食時間では改ページしない）。
/// </summary>
public sealed class AcceptanceRecordPdfService
{
    private const int TemplateRowCount = 14;
    private const int RowsPerPage = 14;

    private readonly JuicePdfService _juicePdf;

    public AcceptanceRecordPdfService(JuicePdfService juicePdf)
    {
        _juicePdf = juicePdf;
    }

    /// <summary>ヘッダー・改ページのグループキー（喫食時間は含めない）。</summary>
    private readonly record struct HeaderGroupKey(
        string FacilityName,
        DateOnly? ShipDate,
        DateOnly? DeliveryDate);

    /// <summary>左上テンプレの納品日・納品時刻は手書き欄として空欄表示する。</summary>
    private const string HeaderDeliveryDateFillIn = "【　　】";

    private const string HeaderDeliveryTimeFillIn = "【　　】";

    public byte[] GeneratePdf(
        string rxzTemplatePath,
        IReadOnlyList<AcceptanceRecordPdfLineModel> lines,
        string? headerLocationFallback,
        string? headerOutDateFallback)
    {
        if (lines == null || lines.Count == 0)
            return Array.Empty<byte>();

        var pageChunks = SplitIntoPages(lines);
        if (pageChunks.Count == 0)
            return Array.Empty<byte>();

        var printNow = DateTime.Now;
        var pages = new List<Dictionary<string, string>>();

        var totalPages = pageChunks.Count;
        for (var i = 0; i < pageChunks.Count; i++)
        {
            var chunk = pageChunks[i];
            var pageNum = i + 1;
            var tags = BuildPageTagValues(chunk, headerLocationFallback, headerOutDateFallback);
            JuicePdfService.AddPrintTags(tags, printNow, pageNum, totalPages);
            tags["PRINTPAGE"] = $"{pageNum}/{totalPages}";
            pages.Add(tags);
        }

        return _juicePdf.GeneratePdfMultiPage(rxzTemplatePath, pages, "検収の記録簿");
    }

    /// <summary>
    /// 施設名 → 出荷日 → 納品日 → 喫食時間 → 明細ID でソート後、
    /// 施設・出荷日・納品日の組が変わるか 14 行超で改ページする。
    /// </summary>
    internal static List<List<AcceptanceRecordPdfLineModel>> SplitIntoPages(IReadOnlyList<AcceptanceRecordPdfLineModel> lines)
    {
        var sorted = lines
            .OrderBy(l => l.DeliveryLocationName ?? "", StringComparer.Ordinal)
            .ThenBy(l => l.PlannedShipDate ?? DateOnly.MaxValue)
            .ThenBy(l => l.PlannedDeliveryDate ?? DateOnly.MaxValue)
            .ThenBy(l => l.SlotDisplay ?? "", StringComparer.Ordinal)
            .ThenBy(l => l.SalesOrderLineId)
            .ToList();

        var pages = new List<List<AcceptanceRecordPdfLineModel>>();
        List<AcceptanceRecordPdfLineModel>? current = null;
        HeaderGroupKey? keyOnPage = null;

        foreach (var line in sorted)
        {
            var key = new HeaderGroupKey(
                line.DeliveryLocationName ?? "",
                line.PlannedShipDate,
                line.PlannedDeliveryDate);

            var needNewPage = current == null
                              || current.Count >= RowsPerPage
                              || keyOnPage != key;

            if (needNewPage)
            {
                current = new List<AcceptanceRecordPdfLineModel>();
                pages.Add(current);
                keyOnPage = key;
            }

            current!.Add(line);
        }

        return pages;
    }

    internal static Dictionary<string, string> BuildPageTagValues(
        IReadOnlyList<AcceptanceRecordPdfLineModel> chunk,
        string? headerLocationFallback,
        string? headerOutDateFallback)
    {
        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var head = chunk.Count > 0 ? chunk[0] : null;
        var loc = (head?.DeliveryLocationName ?? "").Trim();
        tags["LOCATION"] = string.IsNullOrEmpty(loc) ? (headerLocationFallback ?? "") : loc;

        var outDt = head?.PlannedShipDate?.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture) ?? "";
        tags["OUTDATE"] = string.IsNullOrEmpty(outDt) ? (headerOutDateFallback ?? "") : outDt;

        tags["DELVDATE"] = HeaderDeliveryDateFillIn;
        tags["DELVTIME"] = HeaderDeliveryTimeFillIn;
        tags["CARTEMP"] = "";
        tags["ITEMTEMP00"] = "";
        tags["ITEMTEMP01"] = "";
        tags["COLDTEMP00"] = "";
        tags["COLDTEMP01"] = "";

        for (var i = 0; i < TemplateRowCount; i++)
        {
            var nn = i.ToString("D2");
            tags[$"ITEMNM{nn}"] = "";
            tags[$"QUANTITYUNIT{nn}"] = "";
            tags[$"EATDATE{nn}"] = "";
            tags[$"EATTIME{nn}"] = "";
            tags[$"ITEMNUM{nn}"] = "";
            tags[$"COUNT{nn}"] = "";
            tags[$"ALLQUN{nn}"] = "";
            tags[$"COMMENT{nn}"] = "";
            tags[$"LIMIT{nn}"] = "";
            tags[$"AMOUNT{nn}"] = "";
            tags[$"ADD{nn}"] = "";
        }

        for (var i = 0; i < chunk.Count && i < RowsPerPage; i++)
        {
            var r = chunk[i];
            var nn = i.ToString("D2");
            var code = (r.ItemCode ?? "").Trim();
            var name = (r.ItemName ?? "").Trim();

            tags[$"ITEMNM{nn}"] = name;
            tags[$"EATDATE{nn}"] = r.EatDateDisplay ?? "";
            tags[$"EATTIME{nn}"] = r.SlotDisplay ?? "";
            tags[$"ITEMNUM{nn}"] = code;
            tags[$"COUNT{nn}"] = r.MealCountDisplay ?? "";
            tags[$"ALLQUN{nn}"] = r.TotalQtyDisplay ?? "";
            tags[$"QUANTITYUNIT{nn}"] = r.UnitName ?? "";
        }

        return tags;
    }
}
