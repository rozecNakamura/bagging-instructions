using BaggingInstructions.Api.DTOs;
using static BaggingInstructions.Api.ProductionInstructionReportKinds;

namespace BaggingInstructions.Api.Services;

/// <summary>
/// 生産指示書_がんもの炊き合わせ.rxz 用 PDF。主材料 7 + 副材料 9 行／ページ。
/// </summary>
public sealed class GanmonoTakiaiProductionInstructionPdfService
{
    public const int MainMaterialSlots = 7;
    public const int SubMaterialSlots = 9;
    public const int MaxMaterialsPerPage = MainMaterialSlots + SubMaterialSlots;

    private readonly JuicePdfService _juicePdf;

    public GanmonoTakiaiProductionInstructionPdfService(JuicePdfService juicePdf)
    {
        _juicePdf = juicePdf;
    }

    public byte[] GeneratePdf(string rxzTemplatePath, IReadOnlyList<ProductionInstructionPdfLineModel> lines)
    {
        if (lines == null || lines.Count == 0)
            return Array.Empty<byte>();

        var pageTagList = ProductionInstructionRxzPaging.BuildMultiPageTagDictionaries(
            lines,
            MaxMaterialsPerPage,
            BuildPageTagDictionary);

        if (pageTagList.Count == 0)
            return Array.Empty<byte>();

        var printNow = DateTime.Now;
        var totalPages = pageTagList.Count;
        for (var i = 0; i < pageTagList.Count; i++)
            JuicePdfService.AddPrintTags(pageTagList[i], printNow, i + 1, totalPages);

        return _juicePdf.GeneratePdfMultiPage(rxzTemplatePath, pageTagList, GanmonoTakiaiPdfDocumentTitle);
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
            ApplyMainMaterialRow(tags, i, materialChunk[i]);

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
        tags[$"CUTITEMNM{nn}"] = ProductionInstructionRxzTagTexts.SpecOrItemName(row);
        tags[$"FILLQUN{nn}"] = row.ChildRequiredQtyDisplay ?? "";
        tags[$"USEQUN{nn}"] = ProductionInstructionRxzTagTexts.FormatQtyWithUnit(row.ChildRequiredQtyDisplay, row.ChildUnitName);
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
        tags[$"SUBCUTITEMNM{nn}"] = ProductionInstructionRxzTagTexts.SpecOrItemName(row);
        tags[$"SUBFILLQUN{nn}"] = row.ChildRequiredQtyDisplay ?? "";
        tags[$"SUBUSEQUN{nn}"] = ProductionInstructionRxzTagTexts.FormatQtyWithUnit(row.ChildRequiredQtyDisplay, row.ChildUnitName);
    }

    private static Dictionary<string, string> CreateEmptyDataTags(StringComparer comparer)
    {
        var d = new Dictionary<string, string>(comparer);
        foreach (var name in TemplateDataFieldNames.All)
            d[name] = "";
        return d;
    }

    private static class TemplateDataFieldNames
    {
        internal static readonly string[] All = BuildAll().ToArray();

        private static IEnumerable<string> BuildAll()
        {
            yield return "LOTNO";
            yield return "HEATTEMP";
            yield return "HEATTIME";
            yield return "VACPACK";
            yield return "VACSETNO";
            yield return "VACSTOPPOINT";
            yield return "SEALSETVAL";
            yield return "SPEED";
            yield return "XRAYSET_1";
            yield return "PACKLOCATION";
            yield return "PACKNAME_1";
            yield return "PACKSIZE_1";
            yield return "PACKBBD";
            yield return "PACKPRINT";
            yield return "MANAGERNAME";
            yield return "FILLQUNSUM";
            yield return "SUBFILLQUNSUM";
            yield return "COMPRATESUM";
            yield return "SUBUSEQUNSUM";
            yield return "SUBMATYIELD";
            yield return "ITEMPARCD";
            yield return "ITEMPARNM";
            yield return "ITEMMAKEDATE";
            yield return "ITEMCLASS";
            yield return "NEEDSQUN";

            for (var n = 0; n <= 6; n++)
                yield return $"NEEDSUNIT{n:D2}";

            for (var n = 0; n <= 8; n++)
                yield return $"ONEFIRSTUNIT{n:D2}";

            for (var i = 0; i < MainMaterialSlots; i++)
            {
                var nn = i.ToString("D2");
                yield return $"MATCD{nn}";
                yield return $"MATNM{nn}";
                yield return $"YIELD{nn}";
                yield return $"CUTITEMNM{nn}";
                yield return $"FILLQUN{nn}";
                yield return $"USEQUN{nn}";
                yield return $"BBD{nn}";
            }

            for (var i = 0; i < MainMaterialSlots; i++)
                yield return $"ONESIXTH{i:D2}";

            for (var j = 0; j < SubMaterialSlots; j++)
            {
                var nn = j.ToString("D2");
                yield return $"SUBMATCD{nn}";
                yield return $"SUBMATNM{nn}";
                yield return $"COMPRATE{nn}";
                yield return $"SUBCUTITEMNM{nn}";
                yield return $"SUBBBD{nn}";
                yield return $"SUBUSEQUN{nn}";
                yield return $"SUBFILLQUN{nn}";
                yield return $"ONEFIRST{nn}";
            }
        }
    }
}
