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

    [Column("itemid")]
    public long? ItemId { get; set; }

    [Column("quantity")]
    public decimal Quantity { get; set; }

    [Column("quantityinput")]
    public decimal? QuantityInput { get; set; }

    [Column("unitcode")]
    public string? UnitCode { get; set; }

    [Column("unitcodeinput")]
    public string? UnitCodeInput { get; set; }

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

    public virtual SalesOrder SalesOrder { get; set; } = null!;
    public virtual Item? Item { get; set; }
    public virtual CustomerItem? CustomerItem { get; set; }
    public virtual SalesOrderLineAddinfo? Addinfo { get; set; }
}
