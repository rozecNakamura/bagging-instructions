using System.ComponentModel.DataAnnotations.Schema;

namespace BaggingInstructions.Api.Entities;

[Table("customeritem")]
public class CustomerItem
{
    [Column("customeritemid")]
    public long CustomerItemId { get; set; }

    [Column("customerid")]
    public long CustomerId { get; set; }

    [Column("itemid")]
    public long ItemId { get; set; }

    [Column("customercode")]
    public string? CustomerCode { get; set; }

    [Column("customername")]
    public string? CustomerName { get; set; }

    [Column("customershortname")]
    public string? CustomerShortName { get; set; }

    [Column("status")]
    public string? Status { get; set; }

    public virtual Customer? Customer { get; set; }
    public virtual Item? Item { get; set; }
}
