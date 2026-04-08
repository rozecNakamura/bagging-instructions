using System.ComponentModel.DataAnnotations.Schema;

namespace BaggingInstructions.Api.Entities;

[Table("workcenter")]
public class Workcenter
{
    /// <summary>サロゲート ID（主キーは <see cref="WorkcenterCode"/>）。craftlineax は bigint。</summary>
    [Column("workcenterid")]
    public long? WorkcenterId { get; set; }

    [Column("workcentercode")]
    public string WorkcenterCode { get; set; } = "";

    [Column("workcentername")]
    public string? WorkcenterName { get; set; }

    [Column("groupid")]
    public long? GroupId { get; set; }

    [Column("sortorder")]
    public int? SortOrder { get; set; }
}
