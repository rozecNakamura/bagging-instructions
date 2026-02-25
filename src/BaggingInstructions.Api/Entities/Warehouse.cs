using System.ComponentModel.DataAnnotations.Schema;

namespace BaggingInstructions.Api.Entities;

[Table("warehouses")]
public class Warehouse
{
    [Column("warehouseid")]
    public long WarehouseId { get; set; }

    [Column("warehousecode")]
    public string? WarehouseCode { get; set; }

    [Column("warehousename")]
    public string? WarehouseName { get; set; }

    [Column("postalcode")]
    public string? PostalCode { get; set; }

    [Column("prefecture")]
    public string? Prefecture { get; set; }

    [Column("addressline1")]
    public string? AddressLine1 { get; set; }

    [Column("addressline2")]
    public string? AddressLine2 { get; set; }

    [Column("phone")]
    public string? Phone { get; set; }

    [Column("sortorder")]
    public int? SortOrder { get; set; }

    [Column("note")]
    public string? Note { get; set; }

    [Column("status")]
    public string? Status { get; set; }
}
