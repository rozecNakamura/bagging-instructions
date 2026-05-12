using BaggingInstructions.Api.DTOs;

namespace BaggingInstructions.Api.Services;

/// <summary>
/// 調味液配合表.rxz を用いた PDF。日付・作業区・便ごとにページ塊を分ける。
/// </summary>
public sealed class ProductionInstructionPdfService
{
    // 調味液配合表のレイアウトは調理指示書と同系統（00〜12行など）なので、同じ行数・ページング設定を用いる。
    private const int TemplateRowCount = 14;
    private const int RowsPerPage = 12;

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
                var tags = BuildPageTagValues(chunk, grp.Key.SlotDisplay, grp.Key.NeedDate, grp.Key.WorkcenterName);
                JuicePdfService.AddPrintTags(tags, printNow, pageNum, totalPages);
                tags["PRINTPAGE"] = $"{pageNum}/{totalPages}";
                pages.Add(tags);
            }
        }

        return _juicePdf.GeneratePdfMultiPage(rxzTemplatePath, pages, "調味液配合表");
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

            tags[$"ITEMPALNUM{nn}"] = r.ParentItemCode ?? string.Empty;
            tags[$"ORDERNO{nn}"] = (r.OrderNo ?? "").Trim();
            tags[$"ITEMPALNM{nn}"] = r.ParentItemName ?? string.Empty;
            tags[$"ITEMCHINUM{nn}"] = r.ChildItemCode ?? string.Empty;
            tags[$"ITEMCHINM{nn}"] = r.ChildItemName ?? string.Empty;
            tags[$"MAKEQUNPLAN{nn}"] = r.PlannedQuantityDisplay ?? string.Empty;
            tags[$"USEQUNPLAN{nn}"] = r.ChildRequiredQtyDisplay ?? string.Empty;
            tags[$"UNITPAR{nn}"] = (r.PlanUnitName ?? "").Trim();
            tags[$"UNITCHI{nn}"] = (r.ChildUnitName ?? "").Trim();
            tags[$"ITEMSPEC{nn}"] = r.ChildSpec ?? string.Empty;
        }

        return tags;
    }
}

