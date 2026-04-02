using System.Globalization;

namespace BaggingInstructions.Api.Services;

/// <summary>
/// Parent planned qty for 調理指示書: convert ordertable qty toward unit 0 using item additional info,
/// then optionally express in procurement unit (item.unitcode1 → unit row + car1).
/// </summary>
public static class CookingInstructionQuantity
{
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
    /// PDF display qty/unit. If a procurement unit name is present (from item.unitcode1 → unit), optionally divide unit-0 qty by car1.
    /// </summary>
    public static (decimal DisplayQty, string UnitName) ParentDisplayForPdf(
        decimal qtyInUnit0,
        string? procurementUnitName,
        string unit0Name,
        decimal? iaCar1)
    {
        if (!string.IsNullOrWhiteSpace(procurementUnitName))
        {
            var f = iaCar1 ?? 0m;
            var q = f > 0 ? qtyInUnit0 / f : qtyInUnit0;
            return (q, procurementUnitName.Trim());
        }

        return (qtyInUnit0, unit0Name ?? "");
    }
}
