using BaggingInstructions.Api.DTOs;
using static BaggingInstructions.Api.ProductionInstructionReportKinds;

namespace BaggingInstructions.Api.Services;

/// <summary>
/// 生産指示書_ホイコーロー.rxz 用 PDF。1 オーダーあたり最大 15 明細（主材料 5 + 副材料 10）／ページ。
/// 便・注番順は chomi と同様に並べ、ページを連結する。
/// </summary>
public sealed class HoikoloProductionInstructionPdfService
{
    public const int MainMaterialSlots = 5;
    public const int SubMaterialSlots = 10;
    public const int MaxMaterialsPerPage = MainMaterialSlots + SubMaterialSlots;

    private readonly JuicePdfService _juicePdf;

    public HoikoloProductionInstructionPdfService(JuicePdfService juicePdf)
    {
        _juicePdf = juicePdf;
    }

    internal static List<List<ProductionInstructionPdfLineModel>> GroupLinesForHoikolo(
        IReadOnlyList<ProductionInstructionPdfLineModel> lines)
    {
        return lines
            .GroupBy(l => l.OrderNo ?? "", StringComparer.Ordinal)
            .Select(g => g.ToList())
            .OrderBy(g => g[0].SlotDisplay ?? "", StringComparer.Ordinal)
            .ThenBy(g => ParseOrderKey(g[0].OrderNo))
            .ToList();
    }

    public byte[] GeneratePdf(string rxzTemplatePath, IReadOnlyList<ProductionInstructionPdfLineModel> lines)
    {
        if (lines == null || lines.Count == 0)
            return Array.Empty<byte>();

        var groups = GroupLinesForHoikolo(lines);

        var printNow = DateTime.Now;
        var pageTagList = new List<Dictionary<string, string>>();

        foreach (var grp in groups)
        {
            var header = grp[0];
            var childRows = grp.Where(r => !string.IsNullOrWhiteSpace(r.ChildItemCode)).ToList();
            if (childRows.Count == 0)
            {
                pageTagList.Add(BuildPageTagDictionary(header, Array.Empty<ProductionInstructionPdfLineModel>()));
                continue;
            }

            for (var off = 0; off < childRows.Count; off += MaxMaterialsPerPage)
            {
                var chunk = childRows.Skip(off).Take(MaxMaterialsPerPage).ToList();
                pageTagList.Add(BuildPageTagDictionary(header, chunk));
            }
        }

        if (pageTagList.Count == 0)
            return Array.Empty<byte>();

        var totalPages = pageTagList.Count;
        for (var i = 0; i < pageTagList.Count; i++)
            JuicePdfService.AddPrintTags(pageTagList[i], printNow, i + 1, totalPages);

        return _juicePdf.GeneratePdfMultiPage(rxzTemplatePath, pageTagList, HoikoloPdfDocumentTitle);
    }

    /// <summary>単体テスト用：1 ページ分のタグ辞書を構築する。</summary>
    public static Dictionary<string, string> BuildPageTagDictionary(
        ProductionInstructionPdfLineModel header,
        IReadOnlyList<ProductionInstructionPdfLineModel> materialChunk)
    {
        var tags = CreateEmptyDataTags(StringComparer.OrdinalIgnoreCase);
        tags["ITEMPARCD"] = header.ParentItemCode ?? "";
        tags["ITEMPARNM"] = header.ParentItemName ?? "";
        tags["ITEMMAKEDATE"] = header.NeedDateDisplay ?? "";
        tags["ITEMCLASS"] = header.SlotDisplay ?? "";

        for (var i = 0; i < MainMaterialSlots && i < materialChunk.Count; i++)
        {
            ApplyMainMaterialRow(tags, i, materialChunk[i]);
        }

        for (var j = 0; j < SubMaterialSlots; j++)
        {
            var idx = MainMaterialSlots + j;
            if (idx >= materialChunk.Count)
                break;
            ApplySubMaterialRow(tags, j, materialChunk[idx]);
        }

        return tags;
    }

    private static void ApplyMainMaterialRow(
        Dictionary<string, string> tags,
        int mainIndex,
        ProductionInstructionPdfLineModel row)
    {
        var nn = mainIndex.ToString("D2");
        tags[$"MATCD{nn}"] = row.ChildItemCode ?? "";
        tags[$"MATNM{nn}"] = row.ChildItemName ?? "";
        tags[$"YIELD{nn}"] = row.ChildYieldPercentDisplay ?? "";
        tags[$"CUTITEMNM{nn}"] = SpecOrItemName(row);
        tags[$"FILLQUN{nn}"] = row.ChildRequiredQtyDisplay ?? "";
        tags[$"USEQUN{nn}"] = FormatQtyWithUnit(row.ChildRequiredQtyDisplay, row.ChildUnitName);
    }

    private static void ApplySubMaterialRow(
        Dictionary<string, string> tags,
        int subIndex,
        ProductionInstructionPdfLineModel row)
    {
        var nn = subIndex.ToString("D2");
        tags[$"SUBMATCD{nn}"] = row.ChildItemCode ?? "";
        tags[$"SUBMATNM{nn}"] = row.ChildItemName ?? "";
        tags[$"COMPRATE{nn}"] = row.ChildYieldPercentDisplay ?? "";
        tags[$"SUBITEMNM{nn}"] = SpecOrItemName(row);
        tags[$"SUBFILLQUN{nn}"] = row.ChildRequiredQtyDisplay ?? "";
        tags[$"SUBUSEQUN{nn}"] = FormatQtyWithUnit(row.ChildRequiredQtyDisplay, row.ChildUnitName);
    }

    private static string SpecOrItemName(ProductionInstructionPdfLineModel row) =>
        string.IsNullOrWhiteSpace(row.ChildSpec) ? row.ChildItemName ?? "" : row.ChildSpec;

    private static string FormatQtyWithUnit(string? qty, string? unit)
    {
        var q = (qty ?? "").Trim();
        var u = (unit ?? "").Trim();
        if (q.Length == 0)
            return "";
        return u.Length == 0 ? q : $"{q} {u}";
    }

    internal static long ParseOrderKey(string? orderNo)
    {
        if (long.TryParse(orderNo, out var id))
            return id;
        return 0;
    }

    /// <summary>Label### 以外の Data 名を空で初期化（テンプレに無いキーは無害）。</summary>
    internal static Dictionary<string, string> CreateEmptyDataTags(StringComparer comparer)
    {
        var d = new Dictionary<string, string>(comparer);
        foreach (var name in TemplateDataFieldNames.All)
            d[name] = "";
        return d;
    }

    /// <summary>生産指示書_ホイコーロー.rxz の Data 名（静的ラベル除く）。</summary>
    private static class TemplateDataFieldNames
    {
        internal static readonly string[] All = BuildAll().ToArray();

        private static IEnumerable<string> BuildAll()
        {
            yield return "HEATTEMP";
            yield return "HEATTIME";
            yield return "VACPACK";
            yield return "VACSETNO";
            yield return "VACSETVAL_1";
            yield return "VACSETVAL_2";
            yield return "SEALSETVAL";
            yield return "SPEED";
            yield return "XRAYSET_1";
            yield return "XRAYSET_5";
            yield return "PACKLOCATION";
            yield return "PACKNAME_1";
            yield return "PACKSIZE_1";
            yield return "PACKBBD";
            yield return "PACKPRINT";
            yield return "PACKSIZE_5";
            yield return "PACKNAME_5";
            yield return "SUBYIELD";
            yield return "MANAGERNAME";
            yield return "FILLQUNSUM";
            yield return "SUBUSEQUN11";
            yield return "SUBFILLQUNSUM";
            yield return "ITEMPARCD";
            yield return "ITEMPARNM";
            yield return "ITEMMAKEDATE";
            yield return "ITEMCLASS";

            for (var i = 0; i < MainMaterialSlots; i++)
            {
                var nn = i.ToString("D2");
                yield return $"MATCD{nn}";
                yield return $"MATNM{nn}";
                yield return $"YIELD{nn}";
                yield return $"CUTITEMNM{nn}";
                yield return $"FILLQUN{nn}";
                yield return $"USEQUN{nn}";
                yield return $"FILLQUN_1_{nn}";
                yield return $"FILLQUN_5_{nn}";
                yield return $"BBD{nn}";
            }

            for (var j = 0; j < SubMaterialSlots; j++)
            {
                var nn = j.ToString("D2");
                yield return $"SUBMATCD{nn}";
                yield return $"SUBMATNM{nn}";
                yield return $"COMPRATE{nn}";
                yield return $"SUBITEMNM{nn}";
                yield return $"SUBFILLQUN{nn}";
                yield return $"SUBUSEQUN{nn}";
                yield return $"SUBBBD{nn}";
            }
        }
    }
}
