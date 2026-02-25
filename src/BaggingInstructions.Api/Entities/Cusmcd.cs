using System.ComponentModel.DataAnnotations.Schema;

namespace BaggingInstructions.Api.Entities.Legacy;

[Table("cusmcd")]
public class Cusmcd
{
    [Column("prkey")]
    public long Prkey { get; set; }

    [Column("merfctcd")]
    public string Merfctcd { get; set; } = "";

    [Column("cuscd")]
    public string Cuscd { get; set; } = "";

    [Column("cusitemcd")]
    public string Cusitemcd { get; set; } = "";

    [Column("cusitemnm")]
    public string? Cusitemnm { get; set; } = "";

    [Column("fctcd")]
    public string? Fctcd { get; set; } = "";

    [Column("deptcd")]
    public string? Deptcd { get; set; } = "";

    [Column("itemgr")]
    public string? Itemgr { get; set; } = "";

    [Column("itemcd")]
    public string? Itemcd { get; set; } = "";

    [Column("uuser")]
    public string? Uuser { get; set; } = "";

    [Column("udate")]
    public DateTime? Udate { get; set; }

    [Column("salprc0")]
    public decimal Salprc0 { get; set; }

    [Column("salprc1")]
    public decimal Salprc1 { get; set; }

    [Column("salprc2")]
    public decimal Salprc2 { get; set; }

    [Column("salprc3")]
    public decimal Salprc3 { get; set; }

    [Column("shpblock")]
    public string? Shpblock { get; set; } = "";

    [Column("shpblockqun")]
    public decimal Shpblockqun { get; set; }

    [Column("salmemo1")]
    public string? Salmemo1 { get; set; } = "";

    [Column("salmemo2")]
    public string? Salmemo2 { get; set; } = "";

    [Column("salmemo3")]
    public string? Salmemo3 { get; set; } = "";

    [Column("salmemo4")]
    public string? Salmemo4 { get; set; } = "";

    [Column("shopprcexc0")]
    public decimal Shopprcexc0 { get; set; }

    [Column("shopprcinc0")]
    public decimal Shopprcinc0 { get; set; }

    [Column("disusecusitem")]
    public string? Disusecusitem { get; set; } = "";

    [Column("orgplacecd")]
    public string? Orgplacecd { get; set; } = "";

    [Column("gradecd")]
    public string? Gradecd { get; set; } = "";

    [Column("organic")]
    public string? Organic { get; set; } = "";

    [Column("cusitemkanna")]
    public string? Cusitemkanna { get; set; } = "";

    [Column("dispno")]
    public decimal Dispno { get; set; }

    [Column("jouni")]
    public string? Jouni { get; set; } = "";
}
