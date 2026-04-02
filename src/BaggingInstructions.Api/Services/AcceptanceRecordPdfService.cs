using BaggingInstructions.Api.DTOs;

namespace BaggingInstructions.Api.Services;

/// <summary>
/// 検収の記録簿.rxz を用いた PDF 生成。
/// </summary>
public sealed class AcceptanceRecordPdfService
{
    private const int TemplateRowCount = 14;
    private const int RowsPerPage = 14;

    private readonly JuicePdfService _juicePdf;

    public AcceptanceRecordPdfService(JuicePdfService juicePdf)
    {
        _juicePdf = juicePdf;
    }

    public byte[] GeneratePdf(
        string rxzTemplatePath,
        IReadOnlyList<AcceptanceRecordPdfLineModel> lines,
        string? headerLocation,
        string? headerOutDate,
        string? headerDelvDate)
    {
        if (lines == null || lines.Count == 0)
            return Array.Empty<byte>();

        var printNow = DateTime.Now;
        var pages = new List<Dictionary<string, string>>();

        var totalPages = Math.Max(1, (lines.Count + RowsPerPage - 1) / RowsPerPage);

        var pageNum = 0;
        for (var off = 0; off < lines.Count; off += RowsPerPage)
        {
            var chunk = lines.Skip(off).Take(RowsPerPage).ToList();
            pageNum++;
            var tags = BuildPageTagValues(chunk, headerLocation, headerOutDate, headerDelvDate);
            JuicePdfService.AddPrintTags(tags, printNow, pageNum, totalPages);
            tags["PRINTPAGE"] = $"{pageNum}/{totalPages}";
            pages.Add(tags);
        }

        return _juicePdf.GeneratePdfMultiPage(rxzTemplatePath, pages, "検収の記録簿");
    }

    internal static Dictionary<string, string> BuildPageTagValues(
        IReadOnlyList<AcceptanceRecordPdfLineModel> chunk,
        string? headerLocation,
        string? headerOutDate,
        string? headerDelvDate)
    {
        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        tags["LOCATION"] = headerLocation ?? "";
        tags["OUTDATE"] = headerOutDate ?? "";
        tags["DELVDATE"] = headerDelvDate ?? "";
        tags["DELVTIME"] = "";
        tags["CARTEMP"] = "";
        tags["ITEMTEMP00"] = "";
        tags["ITEMTEMP01"] = "";
        tags["COLDTEMP00"] = "";
        tags["COLDTEMP01"] = "";

        for (var i = 0; i < TemplateRowCount; i++)
        {
            var nn = i.ToString("D2");
            tags[$"ITEMNM{nn}"] = "";
            tags[$"QUANTITYUNIT{nn}"] = "";
            tags[$"EATDATE{nn}"] = "";
            tags[$"EATTIME{nn}"] = "";
            tags[$"ITEMNUM{nn}"] = "";
            tags[$"COUNT{nn}"] = "";
            tags[$"ALLQUN{nn}"] = "";
            tags[$"COMMENT{nn}"] = "";
            tags[$"LIMIT{nn}"] = "";
            tags[$"AMOUNT{nn}"] = "";
            tags[$"ADD{nn}"] = "";
        }

        for (var i = 0; i < chunk.Count && i < RowsPerPage; i++)
        {
            var r = chunk[i];
            var nn = i.ToString("D2");

            tags[$"EATDATE{nn}"] = r.EatDateDisplay ?? "";
            tags[$"EATTIME{nn}"] = r.SlotDisplay ?? "";
            tags[$"ITEMNUM{nn}"] = r.ChildItemText ?? "";
            tags[$"COUNT{nn}"] = r.MealCountDisplay ?? "";
            tags[$"ALLQUN{nn}"] = r.TotalQtyDisplay ?? "";
            tags[$"QUANTITYUNIT{nn}"] = r.UnitName ?? "";
        }

        return tags;
    }
}
