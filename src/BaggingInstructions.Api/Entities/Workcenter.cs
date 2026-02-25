using System.ComponentModel.DataAnnotations.Schema;

namespace BaggingInstructions.Api.Entities;

[Table("workcenter")]
public class Workcenter
{
    [Column("workcenterid")]
    public long WorkcenterId { get; set; }

    [Column("workcentername")]
    public string? WorkcenterName { get; set; }

    [Column("groupid")]
    public long? GroupId { get; set; }

    [Column("sortorder")]
    public int? SortOrder { get; set; }
}
