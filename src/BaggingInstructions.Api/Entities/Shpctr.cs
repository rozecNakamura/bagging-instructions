using System.ComponentModel.DataAnnotations.Schema;

namespace BaggingInstructions.Api.Entities.Legacy;

[Table("shpctr")]
public class Shpctr
{
    [Column("prkey")]
    public long Prkey { get; set; }

    [Column("fctcd")]
    public string Fctcd { get; set; } = "";

    [Column("cuscd")]
    public string Cuscd { get; set; } = "";

    [Column("shpctrcd")]
    public string Shpctrcd { get; set; } = "";

    [Column("shpctrnm")]
    public string? Shpctrnm { get; set; } = "";

    [Column("shpctrabb")]
    public string? Shpctrabb { get; set; } = "";

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

    [Column("stadeal")]
    public string? Stadeal { get; set; } = "";

    [Column("enddeal")]
    public string? Enddeal { get; set; } = "";

    [Column("dispno")]
    public decimal Dispno { get; set; }

    [Column("deldt")]
    public DateTime? Deldt { get; set; }

    [Column("ludate")]
    public DateTime? Ludate { get; set; }

    [Column("uuser")]
    public string? Uuser { get; set; } = "";

    [Column("udate")]
    public DateTime? Udate { get; set; }

    [Column("shpctrcolor")]
    public string? Shpctrcolor { get; set; }

    [Column("shpctrcannm")]
    public string? Shpctrcannm { get; set; } = "";

    [Column("distctrcd")]
    public string? Distctrcd { get; set; } = "";

    [Column("distno")]
    public decimal Distno { get; set; }

    [Column("shpctgcd")]
    public string? Shpctgcd { get; set; } = "";

    [Column("prfarcd")]
    public string? Prfarcd { get; set; } = "";

    [Column("trcd")]
    public string? Trcd { get; set; } = "";

    [Column("shpctranocd1")]
    public string? Shpctranocd1 { get; set; } = "";

    [Column("shpctranocd2")]
    public string? Shpctranocd2 { get; set; } = "";

    [Column("shpctrgrp")]
    public string? Shpctrgrp { get; set; } = "";

    [Column("csvplandelvcd")]
    public string? Csvplandelvcd { get; set; } = "";

    [Column("spectyp01")]
    public string? Spectyp01 { get; set; } = "";

    [Column("shpctrkanna")]
    public string? Shpctrkanna { get; set; } = "";

    [Column("shpctrprntnm")]
    public string? Shpctrprntnm { get; set; } = "";

    [Column("srccompcd")]
    public string? Srccompcd { get; set; } = "";

    [Column("shpctrnm2")]
    public string? Shpctrnm2 { get; set; } = "";

    [Column("shpctrjor")]
    public string? Shpctrjor { get; set; } = "";

    [Column("linecd")]
    public string? Linecd { get; set; } = "";

    [Column("shpctrpiccd")]
    public string? Shpctrpiccd { get; set; } = "";
}
