using System.ComponentModel.DataAnnotations.Schema;
using BaggingInstructions.Api.Entities.Legacy;

namespace BaggingInstructions.Api.Entities.Legacy;

[Table("mbom")]
public class Mbom
{
    [Column("prkey")]
    public long Prkey { get; set; }

    [Column("pfctcd")]
    public string Pfctcd { get; set; } = "";

    [Column("pdeptcd")]
    public string Pdeptcd { get; set; } = "";

    [Column("pitemgr")]
    public string Pitemgr { get; set; } = "";

    [Column("pitemcd")]
    public string Pitemcd { get; set; } = "";

    [Column("proutno")]
    public decimal Proutno { get; set; }

    [Column("pcd1")]
    public decimal Pcd1 { get; set; }

    [Column("pcd2")]
    public decimal Pcd2 { get; set; }

    [Column("cfctcd")]
    public string Cfctcd { get; set; } = "";

    [Column("cdeptcd")]
    public string Cdeptcd { get; set; } = "";

    [Column("citemgr")]
    public string Citemgr { get; set; } = "";

    [Column("citemcd")]
    public string Citemcd { get; set; } = "";

    [Column("amu")]
    public decimal Amu { get; set; }

    [Column("otp")]
    public decimal Otp { get; set; }

    [Column("partyp")]
    public string? Partyp { get; set; } = "";

    [Column("par")]
    public decimal? Par { get; set; }

    [Column("prvtyp")]
    public string? Prvtyp { get; set; } = "";

    [Column("issjor")]
    public string? Issjor { get; set; } = "";

    [Column("memo")]
    public string? Memo { get; set; } = "";

    [Column("mbompic")]
    public string? Mbompic { get; set; } = "";

    [Column("stadt")]
    public string Stadt { get; set; } = "";

    [Column("enddt")]
    public string Enddt { get; set; } = "";

    [Column("uuser")]
    public string? Uuser { get; set; } = "";

    [Column("udate")]
    public DateTime? Udate { get; set; }

    [Column("unset")]
    public string? Unset { get; set; } = "";

    [Column("bbdtset")]
    public string? Bbdtset { get; set; } = "0";

    [Column("planruntgtflg")]
    public string Planruntgtflg { get; set; } = "0";

    [Column("weighqun")]
    public decimal Weighqun { get; set; }

    [Column("throwngrpno")]
    public decimal Throwngrpno { get; set; }

    [Column("ratio")]
    public decimal Ratio { get; set; }

    [Column("weightyp")]
    public string? Weightyp { get; set; } = "";

    [Column("addfst")]
    public decimal Addfst { get; set; }

    [Column("mbomlt")]
    public decimal Mbomlt { get; set; }

    // Navigation: 子品目マスタ
    public virtual ItemLegacy? ChildItem { get; set; }
}
