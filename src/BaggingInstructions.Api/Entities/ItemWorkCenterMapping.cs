using System.ComponentModel.DataAnnotations.Schema;

namespace BaggingInstructions.Api.Entities;

[Table("itemworkcentermapping")]
public class ItemWorkCenterMapping
{
    [Column("itemcd")]
    public string ItemCd { get; set; } = "";

    [Column("workcenterid")]
    public long WorkcenterId { get; set; }

    public virtual Workcenter? Workcenter { get; set; }
}
