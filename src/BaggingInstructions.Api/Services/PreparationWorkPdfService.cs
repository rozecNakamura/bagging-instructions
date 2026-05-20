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
                l.HasProductNo,
                l.TemperatureRange
            })
            .ToList();

        foreach (var group in pageGroups)
        {
            var groupLines = group.ToList();
            ApplyParentItemDisplayOrders(groupLines);
            var pageChunks = SplitIntoPageChunks(groupLines);
            var totalPages = pageChunks.Count;

            for (var pageIdx = 0; pageIdx < pageChunks.Count; pageIdx++)
            {
                var chunk = pageChunks[pageIdx];
                var first = chunk[0];
                var pageNum = pageIdx + 1;
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

    /// <summary>
    /// 同一 DisplayOrder が改ページ境界で分断されないよう、ページチャンクに分割する。
    /// 末尾の行が次ページ先頭と同じ DisplayOrder を持つ場合、その ORDER のブロック全体を次ページへ送る。
    /// ただし 1 ページが全て同一 ORDER の場合（RowsPerPage 行超え）は分割せずそのまま出力する。
    /// </summary>
    private static List<List<PreparationPdfLineModel>> SplitIntoPageChunks(List<PreparationPdfLineModel> groupLines)
    {
        var pages = new List<List<PreparationPdfLineModel>>();
        var off = 0;
        while (off < groupLines.Count)
        {
            var canTake = Math.Min(RowsPerPage, groupLines.Count - off);
            var pageEnd = off + canTake;

            // 満杯ページかつ後続行がある場合のみ ORDER 境界を調整する
            if (canTake == RowsPerPage && pageEnd < groupLines.Count)
            {
                var lastOrder = groupLines[pageEnd - 1].DisplayOrder;
                var nextOrder = groupLines[pageEnd].DisplayOrder;

                if (!string.IsNullOrEmpty(lastOrder) && lastOrder == nextOrder)
                {
                    // 末尾から同じ ORDER のブロック先頭を探す
                    var newEnd = pageEnd - 1;
                    while (newEnd > off && groupLines[newEnd - 1].DisplayOrder == lastOrder)
                        newEnd--;

                    // 1行以上残るなら切り戻す（全行同一 ORDER の場合は切り戻さない）
                    if (newEnd > off)
                        pageEnd = newEnd;
                }
            }

            pages.Add(groupLines.GetRange(off, pageEnd - off));
            off = pageEnd;
        }
        return pages;
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
        tags["ITEMTYPE01"] = chunk[0].HasProductNo ? "袋品" : "その他";
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
            var isFirstInParentGroup = i == 0 || chunk[i - 1].ParentItemcode != r.ParentItemcode;

            tags[$"ITEMPALNUM{nn}"] = isFirstInParentGroup ? r.ParentItemcode : "";
            tags[$"ITEMPALNM{nn}"] = isFirstInParentGroup ? r.ParentItemname : "";
            tags[$"ITEMCHINUM{nn}"] = r.ChildItemcode;
            tags[$"ITEMCHINM{nn}"] = r.ChildItemname;
            tags[$"ORDERNO{nn}"] = isFirstInParentGroup ? r.OrderNo : "";
            tags[$"ORDER{nn}"] = r.DisplayOrder;
            tags[$"STANDARD{nn}"] = r.Standard;
            tags[$"QUANTITY{nn}"] = r.Quantity;
            tags[$"UNIT{nn}"] = r.Unit;
            tags[$"LOCATIONTO{nn}"] = r.WarehouseName;
        }

        return tags;
    }
}
