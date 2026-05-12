using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BaggingInstructions.Api.Entities;

/// <summary>
/// craftlineaxother データベースの m_shisetsu テーブル（施設マスタ）。
/// DDL:
///   CREATE TABLE m_shisetsu (
///     id BIGSERIAL PRIMARY KEY,
///     customer_code  VARCHAR(20)  NOT NULL,
///     location_code  VARCHAR(50)  NOT NULL,
///     sort_order     INT,
///     kihon_shokushu VARCHAR(100),
///     remarks        VARCHAR(500)
///   );
/// </summary>
[Table("m_shisetsu")]
public class MShisetsu
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("customer_code")]
    public string? CustomerCode { get; set; }

    [Column("location_code")]
    public string? LocationCode { get; set; }

    /// <summary>並び順（帳票での店舗表示順）。</summary>
    [Column("sort_order")]
    public int? SortOrder { get; set; }

    /// <summary>基本食種ラベル（「基本」「N」など）。</summary>
    [Column("kihon_shokushu")]
    public string? KihonShokushu { get; set; }

    /// <summary>備考。</summary>
    [Column("remarks")]
    public string? Remarks { get; set; }
}
