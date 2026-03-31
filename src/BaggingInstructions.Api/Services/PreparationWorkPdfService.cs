namespace BaggingInstructions.Api.Services;

/// <summary>
/// 作業前準備書.rxz を用いた PDF。中分類ごとにページ塊を分け、1ページ最大12明細行。
/// </summary>
public class PreparationWorkPdfService
{
    /// <summary>rxz の明細行タグは 00〜13 の 14 行分。未使用行は毎ページ空にする。</summary>
    private const int TemplateRowCount = 14;

    // NOTE: テンプレート下端の切れを避けるため、データは 12 行/ページ（00〜11）まで。12〜13 は空のまま。
    private const int RowsPerPage = 12;
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

        var orderedGroups = lines
            .GroupBy(l => l.MiddleClassificationName)
            .OrderBy(g => g.Key, StringComparer.Ordinal);

        var totalPages = orderedGroups.Sum(g => Math.Max(1, (g.Count() + RowsPerPage - 1) / RowsPerPage));
        if (totalPages < 1) totalPages = 1;

        var pageNum = 0;
        foreach (var grp in orderedGroups)
        {
            var list = grp.ToList();
            for (var off = 0; off < list.Count; off += RowsPerPage)
            {
                var chunk = list.Skip(off).Take(RowsPerPage).ToList();
                pageNum++;
                var tags = BuildPageTagValues(chunk, grp.Key, chunk.FirstOrDefault()?.DateDisplay ?? "");
                JuicePdfService.AddPrintTags(tags, printNow, pageNum, totalPages);
                tags["PRINTPAGE"] = $"{pageNum}/{totalPages}";
                pages.Add(tags);
            }
        }

        return _juicePdf.GeneratePdfMultiPage(rxzTemplatePath, pages, "作業前準備書");
    }

    private static Dictionary<string, string> BuildPageTagValues(
        IReadOnlyList<PreparationPdfLineModel> chunk,
        string middleClassificationName,
        string dateDisplay)
    {
        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        tags["GENRE01"] = middleClassificationName;
        tags["ITEMTYPE01"] = middleClassificationName;
        tags["DATE01"] = dateDisplay;

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
            tags[$"ORDER{nn}"] = (i + 1).ToString();
            tags[$"STANDARD{nn}"] = r.Standard;
            tags[$"QUANTITY{nn}"] = r.Quantity;
            tags[$"UNIT{nn}"] = r.Unit;
            tags[$"LOCATIONTO{nn}"] = r.WarehouseName;
        }

        return tags;
    }
}
