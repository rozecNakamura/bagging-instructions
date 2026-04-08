using System.ComponentModel.DataAnnotations.Schema;

namespace BaggingInstructions.Api.Entities;

[Table("salesorder")]
public class SalesOrder
{
    [Column("salesorderid")]
    public long SalesOrderId { get; set; }

    /// <summary>受注番号表示用（API 契約は文字列）。</summary>
    [NotMapped]
    public string SalesOrderNo => SalesOrderId.ToString();

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
    public string? CreatedBy { get; set; }

    [Column("updatedat")]
    public DateTime? UpdatedAt { get; set; }

    [Column("updatedby")]
    public string? UpdatedBy { get; set; }

    [Column("cancelledat")]
    public DateTime? CancelledAt { get; set; }

    [Column("cancelledby")]
    public string? CancelledBy { get; set; }

    [Column("cancelreason")]
    public string? CancelReason { get; set; }

    public virtual Customer? Customer { get; set; }
    public virtual CustomerDeliveryLocation? CustomerDeliveryLocation { get; set; }
    public virtual ICollection<SalesOrderLine> SalesOrderLines { get; set; } = new List<SalesOrderLine>();
}
