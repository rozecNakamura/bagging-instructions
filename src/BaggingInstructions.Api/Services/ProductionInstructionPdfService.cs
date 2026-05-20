using BaggingInstructions.Api.DTOs;

namespace BaggingInstructions.Api.Services;

/// <summary>
/// 調味液配合表.rxz を用いた PDF。日付・作業区・便ごとにページ塊を分ける。
/// </summary>
public sealed class ProductionInstructionPdfService
{
    // 調味液配合表のレイアウトは調理指示書と同系統（00〜12行など）なので、同じ行数・ページング設定を用いる。
    private const int TemplateRowCount = 14;
    private const int RowsPerPage = 13;

    private readonly JuicePdfService _juicePdf;

    public ProductionInstructionPdfService(JuicePdfService juicePdf)
    {
        _juicePdf = juicePdf;
    }

    public byte[] GeneratePdf(string rxzTemplatePath, IReadOnlyList<ProductionInstructionPdfLineModel> lines)
    {
        if (lines == null || lines.Count == 0)
            return Array.Empty<byte>();

        var printNow = DateTime.Now;
        var pages = new List<Dictionary<string, string>>();

        var orderedGroups = lines
            .GroupBy(l => new
            {
                NeedDate = l.NeedDateDisplay ?? "",
                WorkcenterName = l.WorkcenterName ?? "",
                SlotDisplay = l.SlotDisplay ?? ""
            })
            .OrderBy(g => g.Key.NeedDate, StringComparer.Ordinal)
            .ThenBy(g => g.Key.WorkcenterName, StringComparer.Ordinal)
            .ThenBy(g => g.Key.SlotDisplay, StringComparer.Ordinal);

        // 親品目境界でページを分割してから総ページ数を確定する
        var allChunks = orderedGroups
            .SelectMany(grp => SplitIntoPageChunks(grp.ToList())
                .Select(chunk => (grp.Key, chunk)))
            .ToList();

        var totalPages = Math.Max(1, allChunks.Count);
        var pageNum = 0;

        foreach (var (key, chunk) in allChunks)
        {
            pageNum++;
            var tags = BuildPageTagValues(chunk, key.SlotDisplay, key.NeedDate, key.WorkcenterName);
            JuicePdfService.AddPrintTags(tags, printNow, pageNum, totalPages);
            tags["PRINTPAGE"] = $"{pageNum}/{totalPages}";
            pages.Add(tags);
        }

        return _juicePdf.GeneratePdfMultiPage(rxzTemplatePath, pages, "調味液配合表");
    }

    /// <summary>
    /// 同一親品目が改ページ境界で分断されないようページチャンクに分割する。
    /// ただし 1 ページが全行同一親品目の場合は分割せずそのまま出力する。
    /// </summary>
    private static List<List<ProductionInstructionPdfLineModel>> SplitIntoPageChunks(
        List<ProductionInstructionPdfLineModel> groupLines)
    {
        var result = new List<List<ProductionInstructionPdfLineModel>>();
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
        IReadOnlyList<ProductionInstructionPdfLineModel> chunk,
        string slotDisplay,
        string needDateDisplay,
        string workplaceName = "")
    {
        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Header fields: 調味液配合表は調理指示書と同じく GENRE / DATE / ITEMTYPE を持つ
        tags["GENRE01"] = workplaceName ?? string.Empty;      // 職場名（作業区）
        tags["DATE01"] = needDateDisplay ?? string.Empty;     // 日付：
        tags["ITEMTYPE01"] = slotDisplay ?? string.Empty;     // 製造便：

        for (var i = 0; i < TemplateRowCount; i++)
        {
            var nn = i.ToString("D2");
            tags[$"ITEMPALNM{nn}"] = "";
            tags[$"ITEMCHINM{nn}"] = "";
            tags[$"ITEMPALNUM{nn}"] = "";
            tags[$"ITEMCHINUM{nn}"] = "";
            tags[$"UNITPAR{nn}"] = "";
            tags[$"UNITCHI{nn}"] = "";
            tags[$"ORDERNO{nn}"] = "";
            tags[$"ITEMSPEC{nn}"] = "";
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

            tags[$"ITEMPALNUM{nn}"] = isFirstInParentGroup ? (r.ParentItemCode ?? string.Empty) : string.Empty;
            tags[$"ORDERNO{nn}"] = isFirstInParentGroup ? (r.OrderNo ?? "").Trim() : string.Empty;
            tags[$"ITEMPALNM{nn}"] = isFirstInParentGroup ? (r.ParentItemName ?? string.Empty) : string.Empty;
            tags[$"ITEMCHINUM{nn}"] = r.ChildItemCode ?? string.Empty;
            tags[$"ITEMCHINM{nn}"] = r.ChildItemName ?? string.Empty;
            tags[$"MAKEQUNPLAN{nn}"] = isFirstInParentGroup ? (r.PlannedQuantityDisplay ?? string.Empty) : string.Empty;
            tags[$"USEQUNPLAN{nn}"] = r.ChildRequiredQtyDisplay ?? string.Empty;
            tags[$"UNITPAR{nn}"] = isFirstInParentGroup ? (r.PlanUnitName ?? "").Trim() : string.Empty;
            tags[$"UNITCHI{nn}"] = (r.ChildUnitName ?? "").Trim();
            tags[$"ITEMSPEC{nn}"] = r.ChildSpec ?? string.Empty;
        }

        return tags;
    }
}

