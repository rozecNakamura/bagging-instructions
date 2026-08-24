namespace BaggingInstructions.Api.QueryResults;

/// <summary>
/// 現品票印刷（新）用：最上位完成品から BOM を再帰探索した子孫品目1件（Npgsql 読取り用）。
/// 親情報（ParentItemCode/Name）は起点の最上位完成品を指す。
/// </summary>
public sealed class ProductLabelNewChildSqlRow : ProductLabelOrderSqlRow
{
    /// <summary>親（最上位完成品）から見た BOM 階層。1=直接の子、2=孫 …。</summary>
    public int Depth { get; set; }

    /// <summary>製造便コード（productno から解決）。</summary>
    public string SlotCode { get; set; } = "";

    /// <summary>製造便名称（deliveryslot.slotname、無ければコード）。</summary>
    public string SlotName { get; set; } = "";
}
