using System.ComponentModel.DataAnnotations.Schema;

namespace BaggingInstructions.Api.Entities.Legacy;

[Table("daystoc")]
public class Daystoc
{
    [Column("prkey")]
    public long Prkey { get; set; }

    [Column("fctcd")]
    public string Fctcd { get; set; } = "";

    [Column("deptcd")]
    public string Deptcd { get; set; } = "";

    [Column("itemgr")]
    public string Itemgr { get; set; } = "";

    [Column("itemcd")]
    public string Itemcd { get; set; } = "";

    [Column("loctyp")]
    public string Loctyp { get; set; } = "";

    [Column("loccd")]
    public string Loccd { get; set; } = "";

    [Column("lotno")]
    public string Lotno { get; set; } = "";

    [Column("sheno")]
    public string Sheno { get; set; } = "";

    [Column("bbdt")]
    public string Bbdt { get; set; } = "";

    [Column("stocdt")]
    public string Stocdt { get; set; } = "";

    [Column("actstoc")]
    public decimal Actstoc { get; set; }

    [Column("stocuni0")]
    public decimal Stocuni0 { get; set; }

    [Column("stocuni1")]
    public decimal Stocuni1 { get; set; }

    [Column("stocuni2")]
    public decimal Stocuni2 { get; set; }

    [Column("stocuni3")]
    public decimal Stocuni3 { get; set; }

    [Column("fixflg")]
    public string Fixflg { get; set; } = "";

    [Column("uuser")]
    public string Uuser { get; set; } = "";

    [Column("udate")]
    public DateTime? Udate { get; set; }

    [Column("unitprice")]
    public decimal Unitprice { get; set; }

    [Column("movedt")]
    public DateTime? Movedt { get; set; }

    [Column("rcvdelvtyp")]
    public string Rcvdelvtyp { get; set; } = "";

    [Column("rpkordno")]
    public string Rpkordno { get; set; } = "";

    [Column("rpktyp")]
    public string Rpktyp { get; set; } = "";

    [Column("proddate")]
    public DateTime? Proddate { get; set; }

    [Column("shplock")]
    public string Shplock { get; set; } = "";

    [Column("irrstocuni")]
    public decimal Irrstocuni { get; set; }

    [Column("irrcar")]
    public decimal Irrcar { get; set; }
}
