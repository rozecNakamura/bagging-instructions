using System.ComponentModel.DataAnnotations.Schema;

namespace BaggingInstructions.Api.Entities;

[Table("itemadditionalinformation")]
public class ItemAdditionalInformation
{
    /// <summary>サロゲート ID。DB が text の場合あり（EF 主キーは <see cref="ItemCd"/>）。</summary>
    [Column("itemadditionalinformationid")]
    public string? ItemAdditionalInformationId { get; set; }

    /// <summary>品目コード（item.itemcode へ。主キー）。</summary>
    [Column("itemcode")]
    public string ItemCd { get; set; } = "";

    [Column("std1")]
    public string? Std1 { get; set; }

    [Column("std2")]
    public string? Std2 { get; set; }

    [Column("std3")]
    public string? Std3 { get; set; }

    [Column("car0")]
    public decimal? Car0 { get; set; }

    [Column("car1")]
    public decimal? Car1 { get; set; }

    [Column("car2")]
    public decimal? Car2 { get; set; }

    [Column("car3")]
    public decimal? Car3 { get; set; }

    [Column("steritemprange")]
    public decimal? SterItemPrange { get; set; }

    [Column("steritime")]
    public decimal? SteriTime { get; set; }

    [Column("addinfo01")]
    public string? Addinfo01 { get; set; }

    [Column("addinfo02")]
    public string? Addinfo02 { get; set; }

    // [Column("n_wei")]
    // public decimal? Nwei { get; set; }

    public virtual Item Item { get; set; } = null!;
}
