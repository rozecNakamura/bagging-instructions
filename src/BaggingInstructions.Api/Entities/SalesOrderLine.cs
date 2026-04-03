using System.ComponentModel.DataAnnotations.Schema;

namespace BaggingInstructions.Api.Entities;

[Table("salesorderline")]
public class SalesOrderLine
{
    [Column("salesorderlineid")]
    public long SalesOrderLineId { get; set; }

    [Column("salesorderid")]
    public long SalesOrderId { get; set; }

    [Column("lineno")]
    public int LineNo { get; set; }

    /// <summary>品目コード（item.itemcode へ FK。craftlineax では明細は itemcode で紐づく）。</summary>
    [Column("itemcode")]
    public string? ItemCd { get; set; }

    /// <summary>受注数量（DB: quantity）。</summary>
    [Column("quantity")]
    public decimal Quantity { get; set; }

    /// <summary>数量（単位0）。DB: quantityuni0。</summary>
    [Column("quantityuni0")]
    public decimal? QtyUni0 { get; set; }

    /// <summary>数量（単位1／手配単位）。DB: quantityuni1。</summary>
    [Column("quantityuni1")]
    public decimal? QtyUni1 { get; set; }

    [Column("quantityuni2")]
    public decimal? QtyUni2 { get; set; }

    [Column("quantityuni3")]
    public decimal? QtyUni3 { get; set; }

    [Column("unitprice")]
    public decimal? UnitPrice { get; set; }

    [Column("amount")]
    public decimal? Amount { get; set; }

    [Column("plannedshipdate")]
    public DateOnly? PlannedShipDate { get; set; }

    [Column("planneddeliverydate")]
    public DateOnly? PlannedDeliveryDate { get; set; }

    [Column("productdate")]
    public DateOnly? ProductDate { get; set; }

    [Column("customeritemid")]
    public long? CustomerItemId { get; set; }

    [Column("status")]
    public string? Status { get; set; }

    [Column("remarks")]
    public string? Remarks { get; set; }

    [Column("comment")]
    public string? Comment { get; set; }

    /// <summary>便コード（deliveryslot.slotcode と連携）。</summary>
    [Column("slotcode")]
    public string? SlotCode { get; set; }

    public virtual SalesOrder SalesOrder { get; set; } = null!;
    public virtual Item? Item { get; set; }
    public virtual CustomerItem? CustomerItem { get; set; }
    public virtual SalesOrderLineAddinfo? Addinfo { get; set; }
    public virtual OrderTable? OrderTable { get; set; }
}
