using BaggingInstructions.Api.DTOs;

namespace BaggingInstructions.Api.Services;

/// <summary>
/// 調理指示書.rxz を用いた PDF。便（SlotDisplay）ごとにページ塊を分ける。
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

    public byte[] GeneratePdf(string rxzTemplatePath, IReadOnlyList<CookingInstructionPdfLineModel> lines)
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

        return _juicePdf.GeneratePdfMultiPage(rxzTemplatePath, pages, "調理指示書");
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
            tags[$"UNITPAR{nn}"] = "";
            tags[$"UNITCHI{nn}"] = "";
            tags[$"ORDERNO{nn}"] = "";
        }

        for (var i = 0; i < chunk.Count && i < RowsPerPage; i++)
        {
            var r = chunk[i];
            var nn = i.ToString("D2");

            var parentText = string.IsNullOrEmpty(r.ParentItemCode)
                ? r.ParentItemName
                : $"{r.ParentItemCode} {r.ParentItemName}";
            var childText = string.IsNullOrEmpty(r.ChildItemCode)
                ? r.ChildItemName
                : $"{r.ChildItemCode} {r.ChildItemName}";

            static string ShortenUnit(string? unit)
            {
                if (string.IsNullOrEmpty(unit)) return string.Empty;
                return unit.Length > 4 ? unit[..4] : unit;
            }

            tags[$"ITEMPALNM{nn}"] = parentText ?? string.Empty;
            tags[$"ITEMCHINM{nn}"] = childText ?? string.Empty;
            tags[$"ITEMPALNUM{nn}"] = r.PlannedQuantityDisplay ?? string.Empty;
            tags[$"ITEMCHINUM{nn}"] = r.ChildRequiredQtyDisplay ?? string.Empty;
            tags[$"UNITPAR{nn}"] = ShortenUnit(r.PlanUnitName);
            tags[$"UNITCHI{nn}"] = ShortenUnit(r.ChildUnitName);
            tags[$"ORDERNO{nn}"] = r.OrderNo ?? string.Empty;
        }

        return tags;
    }
}

