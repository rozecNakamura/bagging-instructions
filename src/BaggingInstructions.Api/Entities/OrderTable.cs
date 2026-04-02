using System.ComponentModel.DataAnnotations.Schema;

namespace BaggingInstructions.Api.Entities;

/// <summary>
/// 受注数量・納期など（PACK/MO 等）を保持。salesorderline と関連。
/// </summary>
[Table("ordertable")]
public class OrderTable
{
    [Column("ordertableid")]
    public long OrderTableId { get; set; }

    [Column("salesorderlineid")]
    public long SalesOrderLineId { get; set; }

    [Column("itemcode")]
    public string? ItemCode { get; set; }

    [Column("needdate")]
    public DateOnly? NeedDate { get; set; }

    [Column("releasedate")]
    public DateOnly? ReleaseDate { get; set; }

    [Column("qty")]
    public decimal Qty { get; set; }

    /// <summary>MO/PO 等（調味液配合表仕様は MO のみ）。</summary>
    [Column("ordertype")]
    public string? OrderType { get; set; }

    /// <summary>仕入先コード（supplier マスタ参照）。</summary>
    [Column("suppliercode")]
    public string? SupplierCode { get; set; }

    public virtual SalesOrderLine SalesOrderLine { get; set; } = null!;
}
