using System.ComponentModel.DataAnnotations.Schema;

namespace BaggingInstructions.Api.Entities.Legacy;

[Table("workc")]
public class Workc
{
    [Column("prkey")]
    public long Prkey { get; set; }

    [Column("fctcd")]
    public string? Fctcd { get; set; } = "";

    [Column("wccd")]
    public string? Wccd { get; set; } = "";

    [Column("wcnm")]
    public string? Wcnm { get; set; } = "";

    [Column("stdcap")]
    public decimal Stdcap { get; set; }

    [Column("manrate")]
    public decimal Manrate { get; set; }

    [Column("deldt")]
    public DateTime? Deldt { get; set; }

    [Column("ludate")]
    public DateTime? Ludate { get; set; }

    [Column("uuser")]
    public string? Uuser { get; set; } = "";

    [Column("udate")]
    public DateTime? Udate { get; set; }

    [Column("outmemo")]
    public string? Outmemo { get; set; } = "";

    [Column("exceltempfile")]
    public string? Exceltempfile { get; set; } = "";

    [Column("crtempfile")]
    public string? Crtempfile { get; set; } = "";

    [Column("ordlt")]
    public decimal Ordlt { get; set; }

    [Column("crdispcnt")]
    public decimal Crdispcnt { get; set; }

    [Column("dispno")]
    public decimal Dispno { get; set; }

    [Column("wcinfnm")]
    public string? Wcinfnm { get; set; } = "";

    [Column("wcwhcd")]
    public string? Wcwhcd { get; set; } = "";

    [Column("ordoprnodisptyp")]
    public string? Ordoprnodisptyp { get; set; } = "";

    [Column("workprcptncd")]
    public string? Workprcptncd { get; set; } = "";

    [Column("workcgrpcd")]
    public string? Workcgrpcd { get; set; } = "";

    [Column("statm")]
    public string? Statm { get; set; } = "";

    [Column("endtm")]
    public string? Endtm { get; set; } = "";

    [Column("caprate")]
    public decimal Caprate { get; set; }

    [Column("crtempfile2")]
    public string? Crtempfile2 { get; set; } = "";

    [Column("crdispcnt2")]
    public decimal Crdispcnt2 { get; set; }

    [Column("wktmbdr")]
    public decimal Wktmbdr { get; set; }

    [Column("personbdr")]
    public decimal Personbdr { get; set; }

    [Column("capacity")]
    public decimal? Capacity { get; set; }

    [Column("deptcd")]
    public string? Deptcd { get; set; } = "";

    [Column("monthlyworkplan_display")]
    public string? MonthlyworkplanDisplay { get; set; } = "1";

    [Column("exceltempfile2")]
    public string? Exceltempfile2 { get; set; } = "";

    [Column("crtempfile3")]
    public string? Crtempfile3 { get; set; } = "";

    [Column("crdispcnt3")]
    public decimal Crdispcnt3 { get; set; }

    [Column("workcjor")]
    public string? Workcjor { get; set; } = "";

    [Column("crtempfile4")]
    public string? Crtempfile4 { get; set; } = "";

    [Column("crdispcnt4")]
    public decimal Crdispcnt4 { get; set; }

    [Column("worktmtyp")]
    public string? Worktmtyp { get; set; } = "";

    [Column("prdsiz")]
    public decimal Prdsiz { get; set; }
}
