using System.ComponentModel.DataAnnotations.Schema;

namespace BaggingInstructions.Api.Entities;

[Table("customer")]
public class Customer
{
    [Column("customerid")]
    public long CustomerId { get; set; }

    [Column("customercode")]
    public string? CustomerCode { get; set; }

    [Column("customername")]
    public string? CustomerName { get; set; }

    [Column("customershortname")]
    public string? CustomerShortName { get; set; }

    [Column("customernamekana")]
    public string? CustomerNameKana { get; set; }

    [Column("status")]
    public string? Status { get; set; }

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

    [Column("email")]
    public string? Email { get; set; }

    public virtual ICollection<CustomerDeliveryLocation> DeliveryLocations { get; set; } = new List<CustomerDeliveryLocation>();
}
