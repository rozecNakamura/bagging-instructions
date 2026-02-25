using System.ComponentModel.DataAnnotations.Schema;

namespace BaggingInstructions.Api.Entities;

[Table("bom")]
public class Bom
{
    [Column("bomid")]
    public long BomId { get; set; }

    [Column("parentitemcd")]
    public string? ParentItemCd { get; set; }

    [Column("childitemcd")]
    public string? ChildItemCd { get; set; }

    [Column("inputqty")]
    public decimal InputQty { get; set; }

    [Column("inputunitid")]
    public long? InputUnitId { get; set; }

    [Column("yieldpercent")]
    public decimal YieldPercent { get; set; } = 100;

    [Column("outputqty")]
    public decimal OutputQty { get; set; } = 1;

    [Column("productionorder")]
    public decimal? ProductionOrder { get; set; }

    [Column("startdate")]
    public DateOnly? StartDate { get; set; }

    [Column("enddate")]
    public DateOnly? EndDate { get; set; }

    [Column("memo")]
    public string? Memo { get; set; }

    public virtual Item? ChildItem { get; set; }
}
