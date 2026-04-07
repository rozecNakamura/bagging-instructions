using BaggingInstructions.Api.DTOs;
using static BaggingInstructions.Api.ProductionInstructionReportKinds;

namespace BaggingInstructions.Api.Services;

/// <summary>
/// 生産指示書_キャベツとウィンナーのソティ.rxz 用 PDF。材料 19 行／ページ（主副の分割なし）。
/// </summary>
public sealed class CabWinnaSotiProductionInstructionPdfService
{
    public const int MaterialSlotsPerPage = 19;

    private readonly JuicePdfService _juicePdf;

    public CabWinnaSotiProductionInstructionPdfService(JuicePdfService juicePdf)
    {
        _juicePdf = juicePdf;
    }

    public byte[] GeneratePdf(string rxzTemplatePath, IReadOnlyList<ProductionInstructionPdfLineModel> lines)
    {
        if (lines == null || lines.Count == 0)
            return Array.Empty<byte>();

        var pageTagList = ProductionInstructionRxzPaging.BuildMultiPageTagDictionaries(
            lines,
            MaterialSlotsPerPage,
            BuildPageTagDictionary);

        if (pageTagList.Count == 0)
            return Array.Empty<byte>();

        var printNow = DateTime.Now;
        var totalPages = pageTagList.Count;
        for (var i = 0; i < pageTagList.Count; i++)
            JuicePdfService.AddPrintTags(pageTagList[i], printNow, i + 1, totalPages);

        return _juicePdf.GeneratePdfMultiPage(rxzTemplatePath, pageTagList, CabWinnaSotiPdfDocumentTitle);
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

        ProductionInstructionRxzTagTexts.ApplyParentItemAddinfoToLowerRxzTags(
            tags,
            header.ParentItemPrintAddinfo,
            ProductionInstructionLowerSectionPdfVariant.CabWinnaSoti);

        for (var i = 0; i < MaterialSlotsPerPage && i < materialChunk.Count; i++)
            ApplyMaterialRow(tags, i, materialChunk[i]);

        var sum = ProductionInstructionRxzTagTexts.SumChildRequiredQtyDisplay(materialChunk);
        tags["FILLQUNSUM"] = sum;
        tags["USEQUNSUM"] = sum;
        tags["FILLQUNSUMUNIT"] = ProductionInstructionRxzTagTexts.CommonChildUnitNameForSum(materialChunk);

        return tags;
    }

    private static void ApplyMaterialRow(
        Dictionary<string, string> tags,
        int index,
        ProductionInstructionPdfLineModel row)
    {
        var nn = index.ToString("D2");
        tags[$"MATCD{nn}"] = row.ChildItemCode ?? "";
        tags[$"MATNM{nn}"] = row.ChildItemName ?? "";
        tags[$"YIELD{nn}"] = row.ChildYieldPercentDisplay ?? "";
        tags[$"CUTITEMNM{nn}"] = ProductionInstructionRxzTagTexts.SpecOrItemName(row);
        tags[$"FILLQUN{nn}"] = row.ChildRequiredQtyDisplay ?? "";
        tags[$"FILLQUNUNIT{nn}"] = (row.ChildUnitName ?? "").Trim();
        tags[$"USEQUN{nn}"] = row.ChildRequiredQtyDisplay ?? "";
        tags[$"USEQUNUNIT{nn}"] = (row.ChildUnitName ?? "").Trim();
        tags[$"BBD{nn}"] = "";
    }

    private static Dictionary<string, string> CreateEmptyDataTags(StringComparer comparer)
    {
        var d = new Dictionary<string, string>(comparer);
        foreach (var name in TemplateDataFieldNames.All)
            d[name] = "";
        return d;
    }

    /// <summary>生産指示書_キャベツとウィンナーのソティ.rxz の Data 名（静的ラベル除く）。</summary>
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
            yield return "WEIGHTCHECK";
            yield return "PACKLOCATION";
            yield return "PACKNAME_1";
            yield return "PACKSIZE_1";
            yield return "PACKBBD";
            yield return "PACKPRINT";
            yield return "MANAGERNAME";
            yield return "FILLQUNSUM";
            yield return "FILLQUNSUMUNIT";
            yield return "USEQUNSUM";
            yield return "DOCMAKEDATE";
            yield return "DOCNO";
            yield return "REVNO";
            yield return "STANDARD";
            yield return "NEEDSQUN";
            yield return "PLANQUN";
            yield return "ITEMYIELD";
            yield return "SAVE";
            yield return "ITEMPARCD";
            yield return "ITEMPARNM";
            yield return "ITEMMAKEDATE";
            yield return "ITEMCLASS";

            for (var i = 0; i < MaterialSlotsPerPage; i++)
            {
                yield return $"ONESIXTHUNIT{i:D2}";
                yield return $"ONESIXTH{i:D2}";
                var nn = i.ToString("D2");
                yield return $"MATCD{nn}";
                yield return $"MATNM{nn}";
                yield return $"YIELD{nn}";
                yield return $"CUTITEMNM{nn}";
                yield return $"FILLQUN{nn}";
                yield return $"FILLQUNUNIT{nn}";
                yield return $"USEQUN{nn}";
                yield return $"USEQUNUNIT{nn}";
                yield return $"BBD{nn}";
            }
        }
    }
}
