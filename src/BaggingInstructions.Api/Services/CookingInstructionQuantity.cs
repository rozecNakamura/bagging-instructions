using System.Globalization;

namespace BaggingInstructions.Api.Services;

/// <summary>
/// ordertable 製造数: <c>qtyuni0</c> / <c>qtyuni1..3</c> があれば優先（各 <c>qtyuniN</c> は <c>item.conversionvalueN</c> で単位0へ）、
/// なければ <c>qty</c> を ia.std/car0 で単位0へ。
/// 表示は <c>qtyuni1</c>＋手配単位名があればそのまま、なければ単位0→手配は <c>conversionvalue1</c> で除算。
/// </summary>
public static class CookingInstructionQuantity
{
    /// <summary>
    /// BOM・所要計算用の親数量（単位0）。<c>qtyuni0</c> 優先、無ければ
    /// <c>qtyuni1×conversionvalue1</c>（unit1→unit0）、<c>qtyuni2×cv2</c>、<c>qtyuni3×cv3</c>、無ければ qty を換算。
    /// </summary>
    public static decimal ResolveParentQtyInUnit0(
        decimal rawOrdertableQty,
        decimal? qtyUni0,
        decimal? qtyUni1,
        decimal? qtyUni2,
        decimal? qtyUni3,
        string? iaStd,
        decimal? iaCar0,
        decimal? conversionValue1,
        decimal? conversionValue2,
        decimal? conversionValue3)
    {
        if (qtyUni0.HasValue)
            return qtyUni0.Value;
        if (qtyUni1.HasValue && conversionValue1 is > 0)
            return qtyUni1.Value * conversionValue1.Value;
        if (qtyUni2.HasValue && conversionValue2 is > 0)
            return qtyUni2.Value * conversionValue2.Value;
        if (qtyUni3.HasValue && conversionValue3 is > 0)
            return qtyUni3.Value * conversionValue3.Value;
        return ToParentQtyInUnit0(rawOrdertableQty, iaStd, iaCar0);
    }

    /// <summary>
    /// PDF「予定製造量」欄。<c>qtyuni1</c> と手配単位名があればその数量・単位で表示。否则 <see cref="ParentDisplayForPdf"/>。
    /// </summary>
    public static (decimal DisplayQty, string UnitName) ParentPlannedQtyDisplay(
        decimal qtyInUnit0,
        decimal? qtyUni1,
        string? procurementUnitName,
        string unit0Name,
        decimal? conversionValue1)
    {
        if (qtyUni1.HasValue && !string.IsNullOrWhiteSpace(procurementUnitName))
            return (qtyUni1.Value, procurementUnitName.Trim());
        return ParentDisplayForPdf(qtyInUnit0, procurementUnitName, unit0Name, conversionValue1);
    }

    /// <summary>Divisor from item additional info (std, then car0), aligned with bagging search detail.</summary>
    public static decimal DivisorFromItemAddInfo(string? std, decimal? car0)
    {
        if (!string.IsNullOrEmpty(std) &&
            decimal.TryParse(std.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var s) &&
            s > 0)
            return s;
        if (car0 is > 0)
            return car0.Value;
        return 1m;
    }

    /// <summary>Manufacturing quantity expressed in parent unit 0.</summary>
    public static decimal ToParentQtyInUnit0(decimal ordertableQty, string? iaStd, decimal? iaCar0)
    {
        var d = DivisorFromItemAddInfo(iaStd, iaCar0);
        if (d == 0) return ordertableQty;
        return ordertableQty / d;
    }

    /// <summary>
    /// 予定製造量の表示: 単位0数量のまま、または手配単位（item.unitcode1 の名称）＋
    /// <c>item.conversionvalue1</c>（unit1→unit0）が揃っているときだけ手配単位へ換算。
    /// 名称のみで換算値未設定のときは誤表示を避け単位0のまま返す。
    /// </summary>
    public static (decimal DisplayQty, string UnitName) ParentDisplayForPdf(
        decimal qtyInUnit0,
        string? procurementUnitName,
        string unit0Name,
        decimal? conversionValue1)
    {
        if (!string.IsNullOrWhiteSpace(procurementUnitName) && conversionValue1 is > 0)
            return (qtyInUnit0 / conversionValue1.Value, procurementUnitName.Trim());

        return (qtyInUnit0, unit0Name ?? "");
    }
}
