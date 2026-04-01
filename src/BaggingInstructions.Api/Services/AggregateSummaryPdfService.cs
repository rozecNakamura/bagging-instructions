using BaggingInstructions.Api.DTOs;

namespace BaggingInstructions.Api.Services;

/// <summary>
/// 集計表.rxz を用いた PDF。倉庫ごとにページ塊を分ける。
/// </summary>
public sealed class AggregateSummaryPdfService
{
    private const int TemplateRowCount = 14;
    private const int RowsPerPage = 12;

    private readonly JuicePdfService _juicePdf;

    public AggregateSummaryPdfService(JuicePdfService juicePdf)
    {
        _juicePdf = juicePdf;
    }

    public byte[] GeneratePdf(string rxzTemplatePath, IReadOnlyList<AggregateSummaryPdfLineModel> lines)
    {
        if (lines.Count == 0)
            return Array.Empty<byte>();

        var printNow = DateTime.Now;
        var pages = new List<Dictionary<string, string>>();

        var orderedGroups = lines
            .GroupBy(l => l.WarehouseName)
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
                var tags = BuildPageTagValues(chunk, grp.Key, chunk.FirstOrDefault()?.ShipDateDisplay ?? "");
                JuicePdfService.AddPrintTags(tags, printNow, pageNum, totalPages);
                tags["PRINTPAGE"] = $"{pageNum}/{totalPages}";
                pages.Add(tags);
            }
        }

        return _juicePdf.GeneratePdfMultiPage(rxzTemplatePath, pages, "集計表");
    }

    private static Dictionary<string, string> BuildPageTagValues(
        IReadOnlyList<AggregateSummaryPdfLineModel> chunk,
        string warehouseName,
        string shipDateDisplay)
    {
        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        tags["WAREHOUSE"] = warehouseName;
        tags["SHIPDATE"] = shipDateDisplay;

        for (var i = 0; i < TemplateRowCount; i++)
        {
            var nn = i.ToString("D2");
            tags[$"ITEMNAME{nn}"] = "";
            tags[$"REPORTNAME{nn}"] = "";
            tags[$"QUANTITY{nn}"] = "";
            tags[$"UNIT{nn}"] = "";
        }

        for (var i = 0; i < chunk.Count && i < RowsPerPage; i++)
        {
            var r = chunk[i];
            var nn = i.ToString("D2");
            tags[$"REPORTNAME{nn}"] = r.ReportItemName;
            tags[$"ITEMNAME{nn}"] = string.IsNullOrEmpty(r.ChildItemCode)
                ? r.ChildItemName
                : $"{r.ChildItemCode}\n{r.ChildItemName}";
            tags[$"QUANTITY{nn}"] = r.Quantity;
            tags[$"UNIT{nn}"] = r.Unit;
        }

        return tags;
    }
}

