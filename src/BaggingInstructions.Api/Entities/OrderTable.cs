using System.ComponentModel.DataAnnotations.Schema;

namespace BaggingInstructions.Api.Entities;

/// <summary>
/// 受注数量（PACK用）を保持するテーブル。salesorderline と 1:1。
/// </summary>
[Table("ordertable")]
public class OrderTable
{
    [Column("salesorderlineid")]
    public long SalesOrderLineId { get; set; }

    [Column("qty")]
    public decimal Qty { get; set; }

    public virtual SalesOrderLine SalesOrderLine { get; set; } = null!;
}
