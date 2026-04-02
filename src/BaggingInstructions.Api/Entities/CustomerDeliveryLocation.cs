using System.ComponentModel.DataAnnotations.Schema;

namespace BaggingInstructions.Api.Entities;

[Table("customerdeliverylocation")]
public class CustomerDeliveryLocation
{
    [Column("deliverylocationid")]
    public long DeliveryLocationId { get; set; }

    /// <summary>得意先コード（DB 列 customercode）。</summary>
    [Column("customercode")]
    public string? CustomerCode { get; set; }

    [Column("locationcode")]
    public string? LocationCode { get; set; }

    [Column("locationname")]
    public string? LocationName { get; set; }

    [Column("locationshortname")]
    public string? LocationShortName { get; set; }

    [Column("postalcode")]
    public string? PostalCode { get; set; }

    [Column("address1")]
    public string? Address1 { get; set; }

    [Column("address2")]
    public string? Address2 { get; set; }

    [Column("phonenumber")]
    public string? PhoneNumber { get; set; }

    [Column("faxnumber")]
    public string? FaxNumber { get; set; }

    [Column("isdefault")]
    public bool IsDefault { get; set; }

    [Column("isactive")]
    public bool IsActive { get; set; } = true;

    [Column("sortorder")]
    public int SortOrder { get; set; }

    public virtual Customer Customer { get; set; } = null!;
    public virtual CustomerDeliveryLocationAddinfo? Addinfo { get; set; }
}
