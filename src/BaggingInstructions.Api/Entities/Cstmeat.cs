using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BaggingInstructions.Api.Entities;

/// <summary>craftlineaxother データベースの cstmeat テーブル</summary>
[Table("cstmeat")]
public class Cstmeat
{
    [Key]
    [Column("cstmeatid")]
    public int CstmeatId { get; set; }

    [Column("info00")]
    public string? Info00 { get; set; }

    [Column("info01")]
    public string? Info01 { get; set; }

    [Column("info02")]
    public string? Info02 { get; set; }

    [Column("info03")]
    public string? Info03 { get; set; }

    [Column("info04")]
    public string? Info04 { get; set; }

    [Column("info05")]
    public string? Info05 { get; set; }

    [Column("info06")]
    public string? Info06 { get; set; }

    [Column("info07")]
    public string? Info07 { get; set; }

    [Column("info08")]
    public string? Info08 { get; set; }

    [Column("info09")]
    public string? Info09 { get; set; }

    [Column("info10")]
    public string? Info10 { get; set; }

    [Column("info11")]
    public string? Info11 { get; set; }

    [Column("info12")]
    public string? Info12 { get; set; }

    [Column("info13")]
    public string? Info13 { get; set; }

    [Column("info14")]
    public string? Info14 { get; set; }

    [Column("info15")]
    public string? Info15 { get; set; }

    [Column("info16")]
    public string? Info16 { get; set; }

    [Column("info17")]
    public string? Info17 { get; set; }

    [Column("info18")]
    public string? Info18 { get; set; }

    [Column("info19")]
    public string? Info19 { get; set; }
}
