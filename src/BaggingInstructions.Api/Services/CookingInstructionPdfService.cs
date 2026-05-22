using BaggingInstructions.Api.DTOs;

namespace BaggingInstructions.Api.Services;

/// <summary>
/// 調理指示書.rxz を用いた PDF。作業区名 + 日付ごとにページ塊を分ける。
/// 規格は <c>STANDARD00</c>〜<c>STANDARD12</c>（子品目の <see cref="CookingInstructionPdfLineModel.Standard"/>）。
/// </summary>
public sealed class CookingInstructionPdfService
{
    private const int TemplateRowCount = 14;
    private const int RowsPerPage = 13;

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
            .GroupBy(l => new PagingKey(
                l.WorkName ?? string.Empty,
                l.NeedDateDisplay ?? string.Empty,
                l.SlotDisplay ?? string.Empty))
            .OrderBy(g => g.Key.WorkName, StringComparer.Ordinal)
            .ThenBy(g => g.Key.NeedDateDisplay, StringComparer.Ordinal)
            .ThenBy(g => g.Key.SlotDisplay, StringComparer.Ordinal);

        var allChunks = orderedGroups
            .SelectMany(grp => SplitIntoPageChunks(grp.ToList())
                .Select(chunk => (grp.Key, chunk)))
            .ToList();

        var totalPages = Math.Max(1, allChunks.Count);
        var pageNum = 0;

        foreach (var (key, chunk) in allChunks)
        {
            pageNum++;
            var tags = BuildPageTagValues(
                chunk,
                key.SlotDisplay,
                key.NeedDateDisplay,
                key.WorkName);
            JuicePdfService.AddPrintTags(tags, printNow, pageNum, totalPages);
            tags["PRINTPAGE"] = $"{pageNum}/{totalPages}";
            pages.Add(tags);
        }

        var title = string.IsNullOrWhiteSpace(documentTitle) ? "調理指示書" : documentTitle.Trim();
        return _juicePdf.GeneratePdfMultiPage(rxzTemplatePath, pages, title);
    }

    private static List<List<CookingInstructionPdfLineModel>> SplitIntoPageChunks(
        List<CookingInstructionPdfLineModel> groupLines)
    {
        var result = new List<List<CookingInstructionPdfLineModel>>();
        var off = 0;
        while (off < groupLines.Count)
        {
            var canTake = Math.Min(RowsPerPage, groupLines.Count - off);
            var pageEnd = off + canTake;

            if (canTake == RowsPerPage && pageEnd < groupLines.Count)
            {
                var lastParent = groupLines[pageEnd - 1].ParentItemCode;
                var nextParent = groupLines[pageEnd].ParentItemCode;

                if (lastParent == nextParent)
                {
                    var newEnd = pageEnd - 1;
                    while (newEnd > off && groupLines[newEnd - 1].ParentItemCode == lastParent)
                        newEnd--;

                    if (newEnd > off)
                        pageEnd = newEnd;
                }
            }

            result.Add(groupLines.GetRange(off, pageEnd - off));
            off = pageEnd;
        }
        return result;
    }

    internal static Dictionary<string, string> BuildPageTagValues(
        IReadOnlyList<CookingInstructionPdfLineModel> chunk,
        string slotDisplay,
        string needDateDisplay,
        string workName = "")
    {
        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Header fields
        tags["GENRE01"] = workName ?? string.Empty;                    // 作業名（classfication3name）：
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
            var isFirstInParentGroup = i == 0 || chunk[i - 1].ParentItemCode != r.ParentItemCode;

            tags[$"ITEMPALNUM{nn}"] = isFirstInParentGroup ? (r.ParentItemCode ?? "").Trim() : string.Empty;
            tags[$"ITEMPALNM{nn}"] = isFirstInParentGroup ? (r.ParentItemName ?? "").Trim() : string.Empty;
            tags[$"ORDERNO{nn}"] = isFirstInParentGroup ? (r.OrderNo ?? "").Trim() : string.Empty;
            tags[$"MAKEQUNPLAN{nn}"] = isFirstInParentGroup ? (r.PlannedQuantityDisplay ?? string.Empty) : string.Empty;

            tags[$"ITEMCHINUM{nn}"] = (r.ChildItemCode ?? "").Trim();
            tags[$"ITEMCHINM{nn}"] = (r.ChildItemName ?? "").Trim();
            if (i <= 12)
                tags[$"STANDARD{nn}"] = (r.Standard ?? "").Trim();
            tags[$"USEQUNPLAN{nn}"] = r.ChildRequiredQtyDisplay ?? string.Empty;
            tags[$"UNITCHI{nn}"] = (r.ChildUnitName ?? "").Trim();

            // 調理指示書.rxz では UNITPAR00/01 の Y がデータ行 1/0 と対応しており、番号が逆。
            var unitParNn = UnitParTagIndexForDataRow(i).ToString("D2");
            tags[$"UNITPAR{unitParNn}"] = isFirstInParentGroup ? (r.PlanUnitName ?? "").Trim() : string.Empty;
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

    private sealed record PagingKey(string WorkName, string NeedDateDisplay, string SlotDisplay);
}

