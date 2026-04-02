using System.ComponentModel.DataAnnotations.Schema;

namespace BaggingInstructions.Api.Entities;

[Table("salesorder")]
public class SalesOrder
{
    [Column("salesorderid")]
    public long SalesOrderId { get; set; }

    /// <summary>受注番号。DB に salesorderno 列が無いため、salesorderid の値を返す。</summary>
    [NotMapped]
    public long SalesOrderNo => SalesOrderId;

    [Column("customercode")]
    public string? CustomerCode { get; set; }

    [Column("customerdeliverylocationcode")]
    public string? CustomerDeliveryLocationCode { get; set; }

    [Column("orderdate")]
    public DateOnly? OrderDate { get; set; }

    [Column("status")]
    public string? Status { get; set; }

    [Column("remarks")]
    public string? Remarks { get; set; }

    [Column("comment")]
    public string? Comment { get; set; }

    [Column("createdat")]
    public DateTime? CreatedAt { get; set; }

    [Column("createdby")]
    public long? CreatedBy { get; set; }

    [Column("updatedat")]
    public DateTime? UpdatedAt { get; set; }

    [Column("updatedby")]
    public long? UpdatedBy { get; set; }

    [Column("cancelledat")]
    public DateTime? CancelledAt { get; set; }

    [Column("cancelledby")]
    public long? CancelledBy { get; set; }

    [Column("cancelreason")]
    public string? CancelReason { get; set; }

    public virtual Customer? Customer { get; set; }
    public virtual CustomerDeliveryLocation? CustomerDeliveryLocation { get; set; }
    public virtual ICollection<SalesOrderLine> SalesOrderLines { get; set; } = new List<SalesOrderLine>();
}
