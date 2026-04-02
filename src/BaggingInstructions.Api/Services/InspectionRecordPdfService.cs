using BaggingInstructions.Api.DTOs;

namespace BaggingInstructions.Api.Services;

/// <summary>
/// 検品記録簿.rxz を用いた PDF 生成サービス。
/// </summary>
public sealed class InspectionRecordPdfService
{
    // 検品記録簿の行レイアウトは他帳票と同様に 00〜 のインデックスを想定し、行数・ページングも同等とする。
    private const int TemplateRowCount = 14;
    private const int RowsPerPage = 12;

    private readonly JuicePdfService _juicePdf;

    public InspectionRecordPdfService(JuicePdfService juicePdf)
    {
        _juicePdf = juicePdf;
    }

    public byte[] GeneratePdf(string rxzTemplatePath, IReadOnlyList<InspectionRecordPdfLineModel> lines)
    {
        if (lines == null || lines.Count == 0)
            return Array.Empty<byte>();

        var printNow = DateTime.Now;
        var pages = new List<Dictionary<string, string>>();

        // 検品記録簿は便などでのグルーピング要件が明示されていないため、
        // 行順のまま単純にページングする。
        var totalPages = Math.Max(1, (lines.Count + RowsPerPage - 1) / RowsPerPage);

        var pageNum = 0;
        for (var off = 0; off < lines.Count; off += RowsPerPage)
        {
            var chunk = lines.Skip(off).Take(RowsPerPage).ToList();
            pageNum++;
            var tags = BuildPageTagValues(chunk);
            JuicePdfService.AddPrintTags(tags, printNow, pageNum, totalPages);
            tags["PRINTPAGE"] = $"{pageNum}/{totalPages}";
            pages.Add(tags);
        }

        return _juicePdf.GeneratePdfMultiPage(rxzTemplatePath, pages, "検品記録簿");
    }

    internal static Dictionary<string, string> BuildPageTagValues(IReadOnlyList<InspectionRecordPdfLineModel> chunk)
    {
        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // ヘッダ部：検品記録簿.rxz のタグ名に合わせて必要であればここで設定する。
        // 具体的なタグ構造が不明なため、行明細のタグのみ初期化・設定する。

        for (var i = 0; i < TemplateRowCount; i++)
        {
            var nn = i.ToString("D2");
            tags[$"DELVDATE{nn}"] = "";
            tags[$"ORDERNO{nn}"] = "";
            tags[$"ITEMCD{nn}"] = "";
            tags[$"ITEMNM{nn}"] = "";
            tags[$"SPEC{nn}"] = "";
            tags[$"QTY{nn}"] = "";
            tags[$"UNIT{nn}"] = "";
            tags[$"DEVIATION{nn}"] = "";
            tags[$"STORAGE{nn}"] = "";
            tags[$"DELVTIME{nn}"] = "";
            tags[$"TEMP{nn}"] = "";
            tags[$"BESTBEFORE{nn}"] = "";
            tags[$"FRESHNESS{nn}"] = "";
            tags[$"OUTERAPP{nn}"] = "";
        }

        for (var i = 0; i < chunk.Count && i < RowsPerPage; i++)
        {
            var r = chunk[i];
            var nn = i.ToString("D2");

            tags[$"DELVDATE{nn}"] = r.DeliveryDateDisplay ?? string.Empty;
            tags[$"ORDERNO{nn}"] = r.OrderNo ?? string.Empty;
            tags[$"ITEMCD{nn}"] = r.ItemCode ?? string.Empty;
            tags[$"ITEMNM{nn}"] = r.ItemName ?? string.Empty;
            tags[$"SPEC{nn}"] = r.Spec ?? string.Empty;
            tags[$"QTY{nn}"] = r.QuantityDisplay ?? string.Empty;
            tags[$"UNIT{nn}"] = r.UnitName ?? string.Empty;
            tags[$"DEVIATION{nn}"] = r.DeviationHandling ?? string.Empty;
            tags[$"STORAGE{nn}"] = r.StorageLocation ?? string.Empty;
            tags[$"DELVTIME{nn}"] = r.DeliveryTime ?? string.Empty;
            tags[$"TEMP{nn}"] = r.TemperatureCheck ?? string.Empty;
            tags[$"BESTBEFORE{nn}"] = r.BestBefore ?? string.Empty;
            tags[$"FRESHNESS{nn}"] = r.FreshnessGrade ?? string.Empty;
            tags[$"OUTERAPP{nn}"] = r.ExternalAppearance ?? string.Empty;
        }

        return tags;
    }
}

