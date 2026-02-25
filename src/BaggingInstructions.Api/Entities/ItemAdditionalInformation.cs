using System.ComponentModel.DataAnnotations.Schema;

namespace BaggingInstructions.Api.Entities;

[Table("itemadditionalinformation")]
public class ItemAdditionalInformation
{
    [Column("itemadditionalinformationid")]
    public long ItemAdditionalInformationId { get; set; }

    [Column("itemid")]
    public long ItemId { get; set; }

    [Column("std")]
    public string? Std { get; set; }

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

    [Column("nwei")]
    public decimal? Nwei { get; set; }

    public virtual Item Item { get; set; } = null!;
}
