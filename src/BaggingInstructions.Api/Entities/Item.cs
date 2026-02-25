using System.ComponentModel.DataAnnotations.Schema;

namespace BaggingInstructions.Api.Entities;

[Table("item")]
public class Item
{
    [Column("itemid")]
    public long ItemId { get; set; }

    [Column("itemcd")]
    public string? ItemCd { get; set; }

    [Column("itemname")]
    public string? ItemName { get; set; }

    [Column("shortname")]
    public string? ShortName { get; set; }

    [Column("activeflag")]
    public bool ActiveFlag { get; set; } = true;

    [Column("effectivefrom")]
    public DateOnly? EffectiveFrom { get; set; }

    [Column("effectiveto")]
    public DateOnly? EffectiveTo { get; set; }

    [Column("unitid0")]
    public long? UnitId0 { get; set; }

    [Column("unitid1")]
    public long? UnitId1 { get; set; }

    [Column("unitid2")]
    public long? UnitId2 { get; set; }

    [Column("unitid3")]
    public long? UnitId3 { get; set; }

    [Column("shelflifedays")]
    public int? ShelflifeDays { get; set; }

    [Column("isstockmanaged")]
    public bool IsStockManaged { get; set; } = true;

    public virtual Unit? Unit0 { get; set; }
    public virtual ItemAdditionalInformation? AdditionalInformation { get; set; }
    public virtual ICollection<ItemWorkCenterMapping> WorkCenterMappings { get; set; } = new List<ItemWorkCenterMapping>();
}
