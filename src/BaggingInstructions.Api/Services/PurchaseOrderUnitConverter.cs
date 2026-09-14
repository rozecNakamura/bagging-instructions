namespace BaggingInstructions.Api.Services;

/// <summary>
/// <c>item.purchaseorderunitcode</c>（発注単位）への数量換算。
/// purchaseorderunitcode には <c>item.unitcode0</c>～<c>unitcode3</c> のいずれかが設定される。
/// <c>conversionvalueN</c> は「単位N→単位0」の換算値（単位N数量×conversionvalueN＝単位0数量）なので、
/// 単位0数量を <c>conversionvalueN</c> で割ると発注単位の数量になる。
/// 未設定・unitcode0～3 のどれとも一致しない・換算値が未設定のときは、
/// 誤表示を避けて単位0のまま（従来どおり）返す。
/// </summary>
public static class PurchaseOrderUnitConverter
{
    /// <summary>単位0数量を発注単位へ換算し、表示する数量と単位名を返す。</summary>
    public static (decimal Qty, string UnitName) FromUnit0(
        decimal qtyInUnit0,
        string? purchaseOrderUnitCode,
        string? unitCode0,
        string? unitCode1,
        string? unitCode2,
        string? unitCode3,
        decimal? conversionValue1,
        decimal? conversionValue2,
        decimal? conversionValue3,
        string? purchaseOrderUnitName,
        string? unit0Name)
    {
        var fallback = (qtyInUnit0, unit0Name ?? "");

        var po = (purchaseOrderUnitCode ?? "").Trim();
        if (po.Length == 0)
            return fallback;

        var name = string.IsNullOrWhiteSpace(purchaseOrderUnitName)
            ? (unit0Name ?? "")
            : purchaseOrderUnitName.Trim();

        if (SameCode(po, unitCode0))
            return (qtyInUnit0, name);
        if (SameCode(po, unitCode1) && conversionValue1 is > 0)
            return (qtyInUnit0 / conversionValue1.Value, name);
        if (SameCode(po, unitCode2) && conversionValue2 is > 0)
            return (qtyInUnit0 / conversionValue2.Value, name);
        if (SameCode(po, unitCode3) && conversionValue3 is > 0)
            return (qtyInUnit0 / conversionValue3.Value, name);

        return fallback;
    }

    private static bool SameCode(string purchaseOrderUnitCode, string? unitCode) =>
        !string.IsNullOrWhiteSpace(unitCode) &&
        string.Equals(purchaseOrderUnitCode, unitCode.Trim(), StringComparison.OrdinalIgnoreCase);
}
