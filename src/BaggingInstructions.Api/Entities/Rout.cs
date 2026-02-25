using System.ComponentModel.DataAnnotations.Schema;

namespace BaggingInstructions.Api.Entities.Legacy;

[Table("rout")]
public class Rout
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

    [Column("linecd")]
    public string Linecd { get; set; } = "";

    [Column("routno")]
    public decimal Routno { get; set; }

    [Column("whcd")]
    public string? Whcd { get; set; }

    [Column("mos")]
    public string? Mos { get; set; }

    [Column("prccd")]
    public string? Prccd { get; set; }

    [Column("prclt")]
    public decimal? Prclt { get; set; }

    [Column("prccap")]
    public decimal? Prccap { get; set; }

    [Column("isptyp")]
    public string? Isptyp { get; set; }

    [Column("arngtm")]
    public decimal? Arngtm { get; set; }

    [Column("proctm")]
    public decimal? Proctm { get; set; }

    [Column("actcd")]
    public string? Actcd { get; set; }

    [Column("manjor")]
    public string? Manjor { get; set; } = "";

    [Column("routstdcos")]
    public decimal? Routstdcos { get; set; }

    [Column("loccd")]
    public string? Loccd { get; set; }

    [Column("unitprice")]
    public decimal? Unitprice { get; set; }

    [Column("uniptyp")]
    public string? Uniptyp { get; set; }

    [Column("ccptyp")]
    public string? Ccptyp { get; set; }

    [Column("ordfm")]
    public string? Ordfm { get; set; }

    [Column("berthcd")]
    public string? Berthcd { get; set; }

    [Column("deldt")]
    public DateTime? Deldt { get; set; }

    [Column("ludate")]
    public DateTime? Ludate { get; set; }

    [Column("uuser")]
    public string? Uuser { get; set; }

    [Column("udate")]
    public DateTime? Udate { get; set; }

    [Column("ordoprno")]
    public decimal Ordoprno { get; set; }

    [Column("prcratio")]
    public decimal Prcratio { get; set; }

    [Column("prcprnttyp")]
    public string? Prcprnttyp { get; set; } = "";

    [Column("metwccd")]
    public string? Metwccd { get; set; } = "";

    [Column("rmvltm")]
    public decimal Rmvltm { get; set; }

    [Column("unitprice1")]
    public decimal Unitprice1 { get; set; }

    [Column("unitprice2")]
    public decimal Unitprice2 { get; set; }

    [Column("unitprice3")]
    public decimal Unitprice3 { get; set; }

    [Column("prcpes")]
    public decimal Prcpes { get; set; }

    [Column("defpresetupmemo")]
    public string? Defpresetupmemo { get; set; } = "";

    [Column("prcitvlt")]
    public decimal Prcitvlt { get; set; }

    public virtual Ware? Ware { get; set; }
    public virtual Workc? Workc { get; set; }
}
