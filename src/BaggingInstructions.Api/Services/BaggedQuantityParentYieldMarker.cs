namespace BaggingInstructions.Api.Services;

/// <summary>
/// craftlineaxother.baggedquantity 上で親出来高のみを保持する行の子品目コード（実 BOM とは重ならない値）。
/// </summary>
public static class BaggedQuantityParentYieldMarker
{
    public const string ChildItemCode = "__BAGGING_PARENT_YIELD__";

    public static bool IsMarkerRow(string? childItemCode) =>
        string.Equals(childItemCode, ChildItemCode, StringComparison.Ordinal);
}
