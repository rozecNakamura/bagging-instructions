using System.ComponentModel.DataAnnotations.Schema;

namespace BaggingInstructions.Api.Entities;

[Table("salesorderlineaddinfo")]
public class SalesOrderLineAddinfo
{
    [Column("salesorderlineaddinfoid")]
    public long SalesOrderLineAddinfoId { get; set; }

    [Column("salesorderlineid")]
    public long SalesOrderLineId { get; set; }

    [Column("addinfo01")]
    public string? Addinfo01 { get; set; }

    [Column("addinfo01name")]
    public string? Addinfo01Name { get; set; }

    [Column("addinfo02")]
    public string? Addinfo02 { get; set; }

    [Column("addinfo02name")]
    public string? Addinfo02Name { get; set; }

    [Column("addinfo03")]
    public string? Addinfo03 { get; set; }

    [Column("addinfo03name")]
    public string? Addinfo03Name { get; set; }

    [Column("addinfo04")]
    public string? Addinfo04 { get; set; }

    [Column("addinfo04name")]
    public string? Addinfo04Name { get; set; }

    [Column("addinfo05")]
    public string? Addinfo05 { get; set; }

    [Column("addinfo05name")]
    public string? Addinfo05Name { get; set; }

    [Column("addinfo06")]
    public string? Addinfo06 { get; set; }

    [Column("addinfo06name")]
    public string? Addinfo06Name { get; set; }

    [Column("addinfo07")]
    public string? Addinfo07 { get; set; }

    [Column("addinfo07name")]
    public string? Addinfo07Name { get; set; }

    [Column("addinfo08")]
    public string? Addinfo08 { get; set; }

    [Column("addinfo08name")]
    public string? Addinfo08Name { get; set; }

    [Column("addinfo09")]
    public string? Addinfo09 { get; set; }

    [Column("addinfo09name")]
    public string? Addinfo09Name { get; set; }

    [Column("addinfo10")]
    public string? Addinfo10 { get; set; }

    [Column("addinfo10name")]
    public string? Addinfo10Name { get; set; }

    public virtual SalesOrderLine SalesOrderLine { get; set; } = null!;
}
