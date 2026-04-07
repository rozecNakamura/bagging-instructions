using System.Globalization;
using System.Collections.Generic;
using BaggingInstructions.Api.DTOs;

namespace BaggingInstructions.Api.Services;

/// <summary>RXZ テンプレート別：下部ブロックのタグ名・ロット行の差異。</summary>
internal enum ProductionInstructionLowerSectionPdfVariant
{
    CabWinnaSoti,
    GanmonoTakiai,
    Hoikolo,
}

/// <summary>
/// 生産指示書系 RXZ への数量・品名テキスト整形（ホイコーロー・がんも等で共用）。
/// </summary>
internal static class ProductionInstructionRxzTagTexts
{
    internal static string SpecOrItemName(ProductionInstructionPdfLineModel row) =>
        string.IsNullOrWhiteSpace(row.ChildSpec) ? row.ChildItemName ?? "" : row.ChildSpec;

    /// <summary>
    /// 合計行の単位表示用。BOM 子行（<see cref="ProductionInstructionPdfLineModel.ChildItemCode"/> あり）のみ対象。
    /// 単位が一種類に揃うときその文字列、混在または行なしは空。
    /// </summary>
    internal static string CommonChildUnitNameForSum(IReadOnlyList<ProductionInstructionPdfLineModel> rows)
    {
        string? first = null;
        foreach (var r in rows)
        {
            if (string.IsNullOrWhiteSpace(r.ChildItemCode))
                continue;
            var u = (r.ChildUnitName ?? "").Trim();
            if (first == null)
                first = u;
            else if (!string.Equals(first, u, StringComparison.Ordinal))
                return "";
        }

        return first ?? "";
    }

    /// <summary>
    /// 明細行の <see cref="ProductionInstructionPdfLineModel.ChildRequiredQtyDisplay"/> を合計し、
    /// 明細と同じ "0.###" 形式で返す。パース可能な値が 1 件も無いときは空文字。
    /// </summary>
    internal static string SumChildRequiredQtyDisplay(IEnumerable<ProductionInstructionPdfLineModel> rows)
    {
        decimal sum = 0;
        var any = false;
        foreach (var r in rows)
        {
            if (TryParseChildRequiredQty(r.ChildRequiredQtyDisplay, out var v))
            {
                sum += v;
                any = true;
            }
        }

        if (!any)
            return "";
        return sum.ToString("0.###", CultureInfo.InvariantCulture);
    }

    internal static bool TryParseChildRequiredQty(string? display, out decimal value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(display))
            return false;
        return decimal.TryParse(
            display.Trim(),
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out value);
    }

    /// <summary>
    /// 親品目の <see cref="ProductionInstructionParentItemAddinfoForPdf"/> を、生産指示書 RXZ 下部の Data 名へ反映する。
    /// マッピングは業務定義（製品名→addinfo05 … 備考→addinfo17、加熱温度/時間、ロットは空欄固定など）に従う。
    /// </summary>
    internal static void ApplyParentItemAddinfoToLowerRxzTags(
        IDictionary<string, string> tags,
        ProductionInstructionParentItemAddinfoForPdf? ia,
        ProductionInstructionLowerSectionPdfVariant variant)
    {
        if (ia == null)
            return;

        tags["PACKNAME_1"] = ia.Addinfo05.Trim();
        tags["PACKSIZE_1"] = ia.Addinfo06.Trim();
        tags["PACKPRINT"] = ia.Addinfo07.Trim();
        tags["VACPACK"] = ia.Addinfo08.Trim();
        tags["VACSETNO"] = ia.Addinfo09.Trim();
        tags["SEALSETVAL"] = ia.Addinfo11.Trim();
        tags["SPEED"] = ia.Addinfo12.Trim();
        tags["PACKLOCATION"] = ia.Addinfo15.Trim();
        tags["PACKBBD"] = ia.Addinfo16.Trim();
        tags["MANAGERNAME"] = ia.Addinfo17.Trim();

        if (variant == ProductionInstructionLowerSectionPdfVariant.GanmonoTakiai)
            tags["VACSTOPPOINT"] = ia.Addinfo10.Trim();

        tags["HEATTEMP"] = ia.SterItemPrange.HasValue
            ? ia.SterItemPrange.Value.ToString("0.###", CultureInfo.InvariantCulture)
            : "";

        tags["HEATTIME"] = ia.SteriTimeSeconds.HasValue
            ? (ia.SteriTimeSeconds.Value / 60m).ToString("0.###", CultureInfo.InvariantCulture)
            : "";

        switch (variant)
        {
            case ProductionInstructionLowerSectionPdfVariant.Hoikolo:
                tags["XRAYSET_1"] = ia.Addinfo13.Trim();
                tags["XRAYSET_5"] = ia.Addinfo14.Trim();
                if (tags.ContainsKey("LOTNO_1"))
                    tags["LOTNO_1"] = "";
                if (tags.ContainsKey("LOTNO_5"))
                    tags["LOTNO_5"] = "";
                break;
            case ProductionInstructionLowerSectionPdfVariant.GanmonoTakiai:
                tags["LOTNO"] = "";
                tags["XRAYSET_1"] = CombineXraySlotText(ia.Addinfo13, ia.Addinfo14);
                break;
            default:
                tags["XRAYSET_1"] = CombineXraySlotText(ia.Addinfo13, ia.Addinfo14);
                break;
        }
    }

    private static string CombineXraySlotText(string? a, string? b)
    {
        var t1 = (a ?? "").Trim();
        var t2 = (b ?? "").Trim();
        if (string.IsNullOrEmpty(t1))
            return t2;
        if (string.IsNullOrEmpty(t2))
            return t1;
        return $"{t1} / {t2}";
    }
}
