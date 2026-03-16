using System.ComponentModel.DataAnnotations.Schema;

namespace BaggingInstructions.Api.Entities;

[Table("customerdeliverylocationaddinfo")]
public class CustomerDeliveryLocationAddinfo
{
    [Column("deliverylocationaddinfoid")]
    public long CustomerDeliveryLocationAddinfoId { get; set; }

    [Column("deliverylocationid")]
    public long DeliveryLocationId { get; set; }

    [Column("addinfo01")]
    public string? Addinfo01 { get; set; }

    public virtual CustomerDeliveryLocation CustomerDeliveryLocation { get; set; } = null!;
}
