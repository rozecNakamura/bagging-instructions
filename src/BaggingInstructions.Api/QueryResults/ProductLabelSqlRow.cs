namespace BaggingInstructions.Api.QueryResults;

/// <summary>ordertable × item の集計クエリ結果（SqlQuery 用）。</summary>
public sealed class ProductLabelSqlRow
{
    public DateOnly NeedDate { get; set; }
    public string? ItemCd { get; set; }
    public string ItemName { get; set; } = "";
    public decimal Qty { get; set; }
}
