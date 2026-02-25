using System.ComponentModel.DataAnnotations.Schema;

namespace BaggingInstructions.Api.Entities.Legacy;

[Table("ware")]
public class Ware
{
    [Column("prkey")]
    public long Prkey { get; set; }

    [Column("fctcd")]
    public string? Fctcd { get; set; } = "";

    [Column("whcd")]
    public string? Whcd { get; set; } = "";

    [Column("whnm")]
    public string? Whnm { get; set; } = "";

    [Column("deldt")]
    public DateTime? Deldt { get; set; }

    [Column("ludate")]
    public DateTime? Ludate { get; set; }

    [Column("uuser")]
    public string? Uuser { get; set; } = "";

    [Column("udate")]
    public DateTime? Udate { get; set; }

    [Column("dispno")]
    public decimal Dispno { get; set; }

    [Column("whinfnm")]
    public string? Whinfnm { get; set; } = "";

    [Column("zip")]
    public string? Zip { get; set; } = "";

    [Column("add1")]
    public string? Add1 { get; set; } = "";

    [Column("add2")]
    public string? Add2 { get; set; } = "";

    [Column("email")]
    public string? Email { get; set; } = "";

    [Column("tel")]
    public string? Tel { get; set; } = "";

    [Column("fax")]
    public string? Fax { get; set; } = "";

    [Column("whpicnm")]
    public string? Whpicnm { get; set; } = "";

    [Column("trsrctempfile")]
    public string? Trsrctempfile { get; set; } = "";

    [Column("trsrcdispcnt")]
    public decimal Trsrcdispcnt { get; set; }

    [Column("trdesttempfile")]
    public string? Trdesttempfile { get; set; } = "";

    [Column("trdestdispcnt")]
    public decimal Trdestdispcnt { get; set; }

    [Column("whgrplcd")]
    public string? Whgrplcd { get; set; } = "";

    [Column("whgrpmcd")]
    public string? Whgrpmcd { get; set; } = "";

    [Column("whtyp")]
    public string? Whtyp { get; set; } = "0";

    [Column("weicap")]
    public decimal Weicap { get; set; }

    [Column("ocptyp")]
    public string? Ocptyp { get; set; } = "";

    [Column("isrecv")]
    public string? Isrecv { get; set; } = "";

    [Column("iscomp")]
    public string? Iscomp { get; set; } = "";

    [Column("iscalc")]
    public string? Iscalc { get; set; } = "";

    [Column("deptcd")]
    public string? Deptcd { get; set; } = "";

    [Column("whprntnm")]
    public string? Whprntnm { get; set; } = "";

    [Column("whprntnm2")]
    public string? Whprntnm2 { get; set; } = "";

    [Column("isrsv")]
    public string? Isrsv { get; set; } = "";

    [Column("isthrown")]
    public string? Isthrown { get; set; } = "";

    [Column("isvalue")]
    public string? Isvalue { get; set; } = "1";

    [Column("inorouttyp")]
    public string? Inorouttyp { get; set; } = "0";

    [Column("rsvstocgrpcd")]
    public string? Rsvstocgrpcd { get; set; } = "";

    [Column("jobordgrp1")]
    public string? Jobordgrp1 { get; set; } = "";

    [Column("disusewh")]
    public string? Disusewh { get; set; } = "0";

    [Column("blocknm")]
    public string? Blocknm { get; set; } = "";

    [Column("colnum")]
    public decimal Colnum { get; set; }

    [Column("depthnum")]
    public decimal Depthnum { get; set; }

    [Column("stepnum")]
    public decimal Stepnum { get; set; }

    [Column("shemanatyp")]
    public string? Shemanatyp { get; set; } = "";

    [Column("linecd")]
    public string? Linecd { get; set; } = "";

    [Column("wareaddinfo01")]
    public string? Wareaddinfo01 { get; set; } = "";

    [Column("wareaddinfo02")]
    public string? Wareaddinfo02 { get; set; } = "";

    [Column("wareaddinfo03")]
    public string? Wareaddinfo03 { get; set; } = "";

    [Column("wareaddinfo04")]
    public string? Wareaddinfo04 { get; set; } = "";

    [Column("wareaddinfo05")]
    public string? Wareaddinfo05 { get; set; } = "";

    [Column("prewhcd")]
    public string? Prewhcd { get; set; } = "";
}
