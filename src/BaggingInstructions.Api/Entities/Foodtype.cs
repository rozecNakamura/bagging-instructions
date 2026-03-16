using System.ComponentModel.DataAnnotations.Schema;

namespace BaggingInstructions.Api.Entities;

/// <summary>craftlineaxother データベースの foodtype テーブル（食種マスタ）</summary>
[Table("foodtype")]
public class Foodtype
{
    [Column("foodtypeid")]
    public int Foodtypeid { get; set; }

    [Column("foodtypecd")]
    public string? Foodtypecd { get; set; }

    [Column("foodtypename")]
    public string? Foodtypename { get; set; }
}
