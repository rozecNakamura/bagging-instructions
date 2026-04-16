using BaggingInstructions.Api.DTOs;

namespace BaggingInstructions.Api.Services;

/// <summary>
/// 調理指示書.rxz を用いた PDF。便（SlotDisplay）ごとにページ塊を分ける。
/// 規格は <c>STANDARD00</c>〜<c>STANDARD12</c>（子品目の <see cref="CookingInstructionPdfLineModel.Standard"/>）。
/// </summary>
public sealed class CookingInstructionPdfService
{
    private const int TemplateRowCount = 14;
    private const int RowsPerPage = 12;

    private readonly JuicePdfService _juicePdf;

    public CookingInstructionPdfService(JuicePdfService juicePdf)
    {
        _juicePdf = juicePdf;
    }

    public byte[] GeneratePdf(string rxzTemplatePath, IReadOnlyList<CookingInstructionPdfLineModel> lines, string? documentTitle = null)
    {
        if (lines == null || lines.Count == 0)
            return Array.Empty<byte>();

        var printNow = DateTime.Now;
        var pages = new List<Dictionary<string, string>>();

        var orderedGroups = lines
            .GroupBy(l => l.SlotDisplay ?? "")
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
                var tags = BuildPageTagValues(chunk, grp.Key, chunk.FirstOrDefault()?.NeedDateDisplay ?? "");
                JuicePdfService.AddPrintTags(tags, printNow, pageNum, totalPages);
                tags["PRINTPAGE"] = $"{pageNum}/{totalPages}";
                pages.Add(tags);
            }
        }

        var title = string.IsNullOrWhiteSpace(documentTitle) ? "調理指示書" : documentTitle.Trim();
        return _juicePdf.GeneratePdfMultiPage(rxzTemplatePath, pages, title);
    }

    internal static Dictionary<string, string> BuildPageTagValues(
        IReadOnlyList<CookingInstructionPdfLineModel> chunk,
        string slotDisplay,
        string needDateDisplay)
    {
        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var header = chunk.FirstOrDefault();
        // Header fields
        tags["GENRE01"] = header?.WorkplaceNames ?? string.Empty;      // 作業名：
        tags["DATE01"] = needDateDisplay ?? string.Empty;              // 日付：
        tags["ITEMTYPE01"] = slotDisplay ?? string.Empty;              // 製造便：

        for (var i = 0; i < TemplateRowCount; i++)
        {
            var nn = i.ToString("D2");
            tags[$"ITEMPALNM{nn}"] = "";
            tags[$"ITEMCHINM{nn}"] = "";
            tags[$"ITEMPALNUM{nn}"] = "";
            tags[$"ITEMCHINUM{nn}"] = "";
            if (i <= 12)
                tags[$"STANDARD{nn}"] = "";
            tags[$"UNITPAR{nn}"] = "";
            tags[$"UNITCHI{nn}"] = "";
            tags[$"ORDERNO{nn}"] = "";
            // 調理指示書.rxz は調味液と同じく製造予定・使用予定の専用列（MAKEQUNPLAN / USEQUNPLAN）を持つ。
            // ここを埋めないと画面上の「予定」列が空のままになる（ITEMPALNUM だけでは別座標のため表示されない）。
            tags[$"MAKEQUNPLAN{nn}"] = "";
            tags[$"MAKEQUNRESULT{nn}"] = "";
            tags[$"USEQUNPLAN{nn}"] = "";
            tags[$"USEQUNRESULT{nn}"] = "";
        }

        for (var i = 0; i < chunk.Count && i < RowsPerPage; i++)
        {
            var r = chunk[i];
            var nn = i.ToString("D2");

            // 親品目注番: rxz では ITEMPALNUM が上寄せ・上余白、ITEMPALNM が下寄せ・下余白の同一セル。
            // 注番は下段（ITEMPALNM）、親品目は上段（ITEMPALNUM）。予定製造量は MAKEQUNPLAN 列。
            var parentName = FormatItemCodeName(r.ParentItemCode, r.ParentItemName);
            var orderNo = (r.OrderNo ?? "").Trim();
            var childText = FormatItemCodeName(r.ChildItemCode, r.ChildItemName);

            tags[$"ITEMPALNM{nn}"] = orderNo;
            tags[$"ITEMCHINM{nn}"] = childText ?? string.Empty;
            tags[$"ITEMPALNUM{nn}"] = parentName ?? string.Empty;
            tags[$"ITEMCHINUM{nn}"] = "";
            if (i <= 12)
                tags[$"STANDARD{nn}"] = (r.Standard ?? "").Trim();
            tags[$"MAKEQUNPLAN{nn}"] = r.PlannedQuantityDisplay ?? string.Empty;
            tags[$"USEQUNPLAN{nn}"] = r.ChildRequiredQtyDisplay ?? string.Empty;

            // 調理指示書.rxz では UNITPAR00/01 の Y がデータ行 1/0 と対応しており、番号が逆。
            // 単位は全文を渡す（JuicePdf が UNITPAR/UNITCHI で ShrinkToFit を強制）。
            var unitParNn = UnitParTagIndexForDataRow(i).ToString("D2");
            tags[$"UNITPAR{unitParNn}"] = (r.PlanUnitName ?? "").Trim();
            tags[$"UNITCHI{nn}"] = (r.ChildUnitName ?? "").Trim();
            tags[$"ORDERNO{nn}"] = "";
        }

        return tags;
    }

    internal static string FormatItemCodeName(string? code, string? name)
    {
        var c = (code ?? "").Trim();
        var n = (name ?? "").Trim();
        if (c.Length == 0) return n;
        if (n.Length == 0) return c;
        return $"{c} {n}";
    }

    /// <summary>親セル上段用: 注番と計画数量を上寄せのまま縦に積む（テスト・他用途）。</summary>
    internal static string BuildParentTopCell(string? orderNo, string? plannedQtyDisplay)
    {
        var o = (orderNo ?? "").Trim();
        var q = (plannedQtyDisplay ?? "").Trim();
        if (o.Length == 0) return q;
        if (q.Length == 0) return o;
        return $"{o}\n{q}";
    }

    /// <summary>調理指示書.rxz の UNITPAR フィールド番号と表の行の対応（00/01 が逆）。</summary>
    internal static int UnitParTagIndexForDataRow(int dataRowIndex) =>
        dataRowIndex switch
        {
            0 => 1,
            1 => 0,
            _ => dataRowIndex
        };
}

