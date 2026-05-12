namespace BaggingInstructions.Api.Services;

/// <summary>
/// カット前準備書 PDF。作業前準備書.rxz テンプレートを流用し、1ページ最大12明細行。
/// フィールドマッピング（作業前準備書テンプレートの再利用）:
///   LOCATIONFROM01 → 製造便
///   GENRE01        → 中分類名
///   DATE01         → 製造日
///   ORDER{nn}      → No.（連番）
///   ITEMPALNUM{nn} → 親品目コード
///   ITEMPALNM{nn}  → 親品目名称
///   ORDERNO{nn}    → 最終完成品名称（注番フィールドを転用）
///   ITEMCHINUM{nn} → 子品目コード
///   ITEMCHINM{nn}  → 子品目名称
///   QUANTITY{nn}   → 数量
///   UNIT{nn}       → 単位
///   LOCATIONTO{nn} → 保管場所
/// </summary>
public class CutPreparationPdfService
{
    private const int TemplateRowCount = 14;
    private const int RowsPerPage = 12;

    private readonly JuicePdfService _juicePdf;

    public CutPreparationPdfService(JuicePdfService juicePdf)
    {
        _juicePdf = juicePdf;
    }

    public byte[] GeneratePdf(string rxzTemplatePath, IReadOnlyList<CutPreparationPdfLineModel> lines)
    {
        if (lines.Count == 0)
            return Array.Empty<byte>();

        var printNow = DateTime.Now;
        var pages = new List<Dictionary<string, string>>();
        var totalPages = Math.Max(1, (lines.Count + RowsPerPage - 1) / RowsPerPage);

        var pageNum = 0;
        for (var off = 0; off < lines.Count; off += RowsPerPage)
        {
            var chunk = lines.Skip(off).Take(RowsPerPage).ToList();
            var first = chunk[0];
            pageNum++;
            var tags = BuildPageTagValues(chunk, first.DateDisplay, first.ManufacturingRoute, first.MiddleClassName);
            JuicePdfService.AddPrintTags(tags, printNow, pageNum, totalPages);
            tags["PRINTPAGE"] = $"{pageNum}/{totalPages}";
            pages.Add(tags);
        }

        return _juicePdf.GeneratePdfMultiPage(rxzTemplatePath, pages, "カット前準備書");
    }

    private static Dictionary<string, string> BuildPageTagValues(
        IReadOnlyList<CutPreparationPdfLineModel> chunk,
        string dateDisplay,
        string mfgRoute,
        string middleClassName)
    {
        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        tags["LOCATIONFROM01"] = mfgRoute;
        tags["GENRE01"] = middleClassName;
        tags["ITEMTYPE01"] = middleClassName;
        tags["DATE01"] = dateDisplay;
        tags["TEMP"] = "";

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
            tags[$"ORDER{nn}"] = (i + 1).ToString();
            tags[$"ITEMPALNUM{nn}"] = r.ParentItemcode;
            tags[$"ITEMPALNM{nn}"] = r.ParentItemname;
            tags[$"ORDERNO{nn}"] = r.FinalProductName;
            tags[$"ITEMCHINUM{nn}"] = r.ChildItemcode;
            tags[$"ITEMCHINM{nn}"] = r.ChildItemname;
            tags[$"QUANTITY{nn}"] = r.Quantity;
            tags[$"UNIT{nn}"] = r.Unit;
            tags[$"LOCATIONTO{nn}"] = r.WarehouseName;
        }

        return tags;
    }
}
