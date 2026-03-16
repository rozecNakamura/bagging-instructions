using System.ComponentModel.DataAnnotations.Schema;

namespace BaggingInstructions.Api.Entities;

/// <summary>craftlineaxother データベースの eattime テーブル（喫食時間マスタ）</summary>
[Table("eattime")]
public class Eattime
{
    [Column("eattimecd")]
    public string Eattimecd { get; set; } = "";

    [Column("eattimename")]
    public string? Eattimename { get; set; }
}
