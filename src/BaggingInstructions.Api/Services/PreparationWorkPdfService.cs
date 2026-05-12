namespace BaggingInstructions.Api.Services;

/// <summary>
/// 作業前準備書.rxz を用いた PDF。明細は製造日・作業区・殺菌温度・注番・親品目・子品目でソートし、1ページ最大12明細行。
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

        var sorted = PreparationWorkReportSort.SortPdfLines(lines);
        var totalPages = Math.Max(1, (sorted.Count + RowsPerPage - 1) / RowsPerPage);

        var pageNum = 0;
        for (var off = 0; off < sorted.Count; off += RowsPerPage)
        {
            var chunk = sorted.Skip(off).Take(RowsPerPage).ToList();
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

        return _juicePdf.GeneratePdfMultiPage(rxzTemplatePath, pages, "作業前準備書");
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
            tags[$"ORDER{nn}"] = (i + 1).ToString();
            tags[$"STANDARD{nn}"] = r.Standard;
            tags[$"QUANTITY{nn}"] = r.Quantity;
            tags[$"UNIT{nn}"] = r.Unit;
            tags[$"LOCATIONTO{nn}"] = r.WarehouseName;
        }

        return tags;
    }
}
