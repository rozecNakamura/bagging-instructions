namespace BaggingInstructions.Api.QueryResults;

/// <summary>現品票 PDF 用：ordertable × item × workcenter × bom × 子品目 × 子品目単位（Npgsql 読取り用）。</summary>
public sealed class ProductLabelOrderSqlRow
{
    public long OrderTableId { get; set; }
    public DateOnly? ReleaseDate { get; set; }
    public string ParentItemCode { get; set; } = "";
    public string ParentItemName { get; set; } = "";
    public decimal Qty { get; set; }
    public string WorkcenterName { get; set; } = "";
    public string ChildItemCode { get; set; } = "";
    public string ChildItemName { get; set; } = "";
    /// <summary>子品目の数量（ordertable.qty × bom.inputqty / bom.outputqty）。子品目なしの場合は ordertable.qty。</summary>
    public decimal ChildQty { get; set; }
    /// <summary>子品目の単位0名称（unit.unitname / unitsymbol）。</summary>
    public string ChildUnitName { get; set; } = "";
    /// <summary>親品目の賞味期限日数（item.shelflifedays）。0 の場合は既定値を使用。</summary>
    public int ShelflifeDays { get; set; }
}
