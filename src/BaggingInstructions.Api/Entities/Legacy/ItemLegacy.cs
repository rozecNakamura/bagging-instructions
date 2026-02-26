using System.ComponentModel.DataAnnotations.Schema;

namespace BaggingInstructions.Api.Entities.Legacy;

/// <summary>現行DB用品目エンティティ（複合キー）。新DB移行後は参照用に残す。</summary>
[Table("item")]
public class ItemLegacy
{
    [Column("prkey")]
    public long Prkey { get; set; }

    [Column("fctcd")]
    public string? Fctcd { get; set; }

    [Column("deptcd")]
    public string? Deptcd { get; set; }

    [Column("itemgr")]
    public string? Itemgr { get; set; }

    [Column("itemcd")]
    public string? Itemcd { get; set; }

    [Column("itemnm")]
    public string? Itemnm { get; set; }

    [Column("std")]
    public string? Std { get; set; }

    [Column("car")]
    public decimal? Car { get; set; }

    [Column("uni0")]
    public string? Uni0 { get; set; }

    [Column("jouni")]
    public string? Jouni { get; set; }

    [Column("strtemp")]
    public string? Strtemp { get; set; }

    [Column("nwei")]
    public decimal Nwei { get; set; }

    /// <summary>工程（rout）。現行DBで item と rout を結合する場合は別途設定。EF ではマッピングしない。</summary>
    [NotMapped]
    public virtual ICollection<Rout> Routs { get; set; } = new List<Rout>();
}
