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

    /// <summary>製造数（単位0）。設定時は qty からの換算より優先。</summary>
    [Column("qtyuni0")]
    public decimal? QtyUni0 { get; set; }

    /// <summary>製造数（手配単位＝item.unitcode1）。表示優先。単位0は item.conversionvalue1 で逆算可能。</summary>
    [Column("qtyuni1")]
    public decimal? QtyUni1 { get; set; }

    [Column("qtyuni2")]
    public decimal? QtyUni2 { get; set; }

    [Column("qtyuni3")]
    public decimal? QtyUni3 { get; set; }

    /// <summary>MO/PO 等（調味液配合表仕様は MO のみ）。</summary>
    [Column("ordertype")]
    public string? OrderType { get; set; }

    /// <summary>仕入先コード（supplier マスタ参照）。</summary>
    [Column("suppliercode")]
    public string? SupplierCode { get; set; }

    public virtual SalesOrderLine SalesOrderLine { get; set; } = null!;
}
