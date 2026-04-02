using BaggingInstructions.Api.DTOs;

namespace BaggingInstructions.Api.Services;

/// <summary>
/// 調味液配合表.rxz を用いた PDF。便（SlotDisplay）ごとにページ塊を分ける。
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

        return _juicePdf.GeneratePdfMultiPage(rxzTemplatePath, pages, "調味液配合表");
    }

    internal static Dictionary<string, string> BuildPageTagValues(
        IReadOnlyList<ProductionInstructionPdfLineModel> chunk,
        string slotDisplay,
        string needDateDisplay)
    {
        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var header = chunk.FirstOrDefault();
        // Header fields: 調味液配合表は調理指示書と同じく GENRE / DATE / ITEMTYPE を持つ
        tags["GENRE01"] = string.Empty;                       // 作業名：今回は空欄
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
            // 調味液配合表.rxz では ITEMPALNM / ORDERNO / ITEMPALNUM が同一セル座標で重なるため、
            // 数量は専用列（MAKEQUNPLAN / USEQUNPLAN）へ出し、親は 1 フィールドにまとめる。
            tags[$"MAKEQUNPLAN{nn}"] = "";
            tags[$"MAKEQUNRESULT{nn}"] = "";
            tags[$"USEQUNPLAN{nn}"] = "";
            tags[$"USEQUNRESULT{nn}"] = "";
        }

        for (var i = 0; i < chunk.Count && i < RowsPerPage; i++)
        {
            var r = chunk[i];
            var nn = i.ToString("D2");

            var codeName = CookingInstructionPdfService.FormatItemCodeName(r.ParentItemCode, r.ParentItemName);
            var order = (r.OrderNo ?? "").Trim();
            var parentText = order.Length == 0
                ? codeName
                : string.IsNullOrEmpty(codeName)
                    ? order
                    : $"{order}\n{codeName}";

            var childText = CookingInstructionPdfService.FormatItemCodeName(r.ChildItemCode, r.ChildItemName);

            static string ShortenUnit(string? unit)
            {
                if (string.IsNullOrEmpty(unit)) return string.Empty;
                return unit.Length > 4 ? unit[..4] : unit;
            }

            tags[$"ITEMPALNM{nn}"] = parentText;
            tags[$"ITEMCHINM{nn}"] = childText;
            tags[$"ITEMPALNUM{nn}"] = "";
            tags[$"ITEMCHINUM{nn}"] = "";
            tags[$"MAKEQUNPLAN{nn}"] = r.PlannedQuantityDisplay ?? string.Empty;
            tags[$"USEQUNPLAN{nn}"] = r.ChildRequiredQtyDisplay ?? string.Empty;
            tags[$"UNITPAR{nn}"] = ShortenUnit(r.PlanUnitName);
            tags[$"UNITCHI{nn}"] = ShortenUnit(r.ChildUnitName);
            tags[$"ORDERNO{nn}"] = "";
            tags[$"ITEMSPEC{nn}"] = r.ChildSpec ?? string.Empty;
        }

        return tags;
    }
}

