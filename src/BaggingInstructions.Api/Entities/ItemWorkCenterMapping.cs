using System.ComponentModel.DataAnnotations.Schema;

namespace BaggingInstructions.Api.Entities;

[Table("itemworkcentermapping")]
public class ItemWorkCenterMapping
{
    [Column("itemcode")]
    public string ItemCd { get; set; } = "";

    [Column("workcentercode")]
    public string WorkcenterCode { get; set; } = "";

    public virtual Workcenter? Workcenter { get; set; }
}
