using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BaggingInstructions.Api.Entities;

/// <summary>craftlineaxother データベースの m_shokushu テーブル（食種マスタ・代表優先順）</summary>
[Table("m_shokushu")]
public class Mshokushu
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("shokushu_code")]
    public string? ShokushuCode { get; set; }

    [Column("shokushu_name")]
    public string? ShokushuName { get; set; }

    [Column("priority_order")]
    public decimal? PriorityOrder { get; set; }

    [Column("seikyu_kubun_code")]
    public string? SeikyuKubunCode { get; set; }
}
