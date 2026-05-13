namespace BaggingInstructions.Api.Services;

/// <summary>
/// 作業前準備書.rxz を用いた PDF。ヘッダ項目が変わるか、1ページ14明細行を超える場合に改頁する。
/// </summary>
public class PreparationWorkPdfService
{
    /// <summary>rxz の明細行タグは 00〜13 の 14 行分。未使用行は毎ページ空にする。</summary>
    private const int TemplateRowCount = 14;

    // データは 14 行/ページ（00〜13）まで出力する。
    private const int RowsPerPage = 14;
    private readonly JuicePdfService _juicePdf;

    public PreparationWorkPdfService(JuicePdfService juicePdf)
    {
        _juicePdf = juicePdf;
    }

    public byte[] GeneratePdf(string rxzTemplatePath, IReadOnlyList<PreparationPdfLineModel> lines)
    {
        if (lines.Count == 0)
            return Array.Empty<byte>();

        var printNow = DateTime.Now;
        var pages = new List<Dictionary<string, string>>();

        var sorted = PreparationWorkReportSort.SortPdfLines(lines);
        var pageGroups = sorted
            .GroupBy(l => new
            {
                l.WorkplaceCode,
                l.DateDisplay,
                l.ManufacturingRouteCode,
                l.MiddleClassificationCode,
                l.TemperatureRange
            })
            .ToList();

        foreach (var group in pageGroups)
        {
            var groupLines = group.ToList();
            ApplyParentItemDisplayOrders(groupLines);
            var totalPages = Math.Max(1, (groupLines.Count + RowsPerPage - 1) / RowsPerPage);

            var pageNum = 0;
            for (var off = 0; off < groupLines.Count; off += RowsPerPage)
            {
                var chunk = groupLines.Skip(off).Take(RowsPerPage).ToList();
                var first = chunk[0];
                pageNum++;
                var tags = BuildPageTagValues(
                    chunk,
                    first.MiddleClassificationName ?? "",
                    first.DateDisplay ?? "",
                    first.WorkplaceName ?? "",
                    first.TemperatureRange ?? "");
                JuicePdfService.AddPrintTags(tags, printNow, pageNum, totalPages);
                tags["PRINTPAGE"] = $"{pageNum}/{totalPages}";
                pages.Add(tags);
            }
        }

        return _juicePdf.GeneratePdfMultiPage(rxzTemplatePath, pages, "作業前準備書");
    }

    private static void ApplyParentItemDisplayOrders(IReadOnlyList<PreparationPdfLineModel> groupLines)
    {
        var orderByParentItem = new Dictionary<string, int>(StringComparer.Ordinal);
        var nextOrder = 1;

        foreach (var line in groupLines)
        {
            var parentKey = line.ParentItemcode ?? string.Empty;
            if (!orderByParentItem.TryGetValue(parentKey, out var displayOrder))
            {
                displayOrder = nextOrder++;
                orderByParentItem[parentKey] = displayOrder;
            }

            line.DisplayOrder = displayOrder.ToString();
        }
    }

    private static Dictionary<string, string> BuildPageTagValues(
        IReadOnlyList<PreparationPdfLineModel> chunk,
        string middleClassificationName,
        string dateDisplay,
        string workplaceName,
        string temperatureRange)
    {
        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        tags["LOCATIONFROM01"] = workplaceName;
        tags["GENRE01"] = middleClassificationName;
        tags["ITEMTYPE01"] = middleClassificationName;
        tags["DATE01"] = dateDisplay;
        tags["TEMP"] = temperatureRange;

        for (var i = 0; i < TemplateRowCount; i++)
        {
            var nn = i.ToString("D2");
            tags[$"ITEMPALNUM{nn}"] = "";
            tags[$"ITEMPALNM{nn}"] = "";
            tags[$"ITEMCHINUM{nn}"] = "";
            tags[$"ITEMCHINM{nn}"] = "";
            tags[$"ORDERNO{nn}"] = "";
            tags[$"ORDER{nn}"] = "";
            tags[$"STANDARD{nn}"] = "";
            tags[$"QUANTITY{nn}"] = "";
            tags[$"UNIT{nn}"] = "";
            tags[$"LOCATIONTO{nn}"] = "";
        }

        for (var i = 0; i < chunk.Count && i < RowsPerPage; i++)
        {
            var r = chunk[i];
            var nn = i.ToString("D2");
            tags[$"ITEMPALNUM{nn}"] = r.ParentItemcode;
            tags[$"ITEMPALNM{nn}"] = r.ParentItemname;
            tags[$"ITEMCHINUM{nn}"] = r.ChildItemcode;
            tags[$"ITEMCHINM{nn}"] = r.ChildItemname;
            tags[$"ORDERNO{nn}"] = r.OrderNo;
            tags[$"ORDER{nn}"] = r.DisplayOrder;
            tags[$"STANDARD{nn}"] = r.Standard;
            tags[$"QUANTITY{nn}"] = r.Quantity;
            tags[$"UNIT{nn}"] = r.Unit;
            tags[$"LOCATIONTO{nn}"] = r.WarehouseName;
        }

        return tags;
    }
}
