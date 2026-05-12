using System.ComponentModel.DataAnnotations.Schema;

namespace BaggingInstructions.Api.Entities;

[Table("itemadditionalinformation")]
public class ItemAdditionalInformation
{
    /// <summary>内部採番 ID。craftlineax は bigint（EF の主キーは <see cref="ItemCd"/>）。</summary>
    [Column("itemadditionalinformationid")]
    public long ItemAdditionalInformationId { get; set; }

    /// <summary>品目コード（item.itemcode へ。主キー）。</summary>
    [Column("itemcode")]
    public string ItemCd { get; set; } = "";

    [Column("car0")]
    public decimal? Car0 { get; set; }

    [Column("car1")]
    public decimal? Car1 { get; set; }

    [Column("car2")]
    public decimal? Car2 { get; set; }

    [Column("car3")]
    public decimal? Car3 { get; set; }

    /// <summary>規格（text）。</summary>
    [Column("std")]
    public string? Std { get; set; }

    [Column("steritemprange")]
    public decimal? SterItemPrange { get; set; }

    [Column("steritime")]
    public decimal? SteriTime { get; set; }

    [Column("addinfo01")]
    public string? Addinfo01 { get; set; }

    [Column("addinfo02")]
    public string? Addinfo02 { get; set; }

    [Column("addinfo03")]
    public string? Addinfo03 { get; set; }

    [Column("addinfo04")]
    public string? Addinfo04 { get; set; }

    [Column("addinfo05")]
    public string? Addinfo05 { get; set; }

    /// <summary>計量器品目コード等（計量器連携 CSV の ITEMCD 等）。</summary>
    [Column("addinfo06")]
    public string? Addinfo06 { get; set; }

    // [Column("n_wei")]
    // public decimal? Nwei { get; set; }

    public virtual Item Item { get; set; } = null!;
}
