namespace BaggingInstructions.Api.Services;

/// <summary>
/// Legacy: 旧「親出来高」行の子品目コード。読込時のみ除外し、実 BOM 行と混ざらないようにする（新規保存はしない）。
/// </summary>
public static class BaggedQuantityParentYieldMarker
{
    public const string ChildItemCode = "__BAGGING_PARENT_YIELD__";

    public static bool IsMarkerRow(string? childItemCode) =>
        string.Equals(childItemCode, ChildItemCode, StringComparison.Ordinal);
}
