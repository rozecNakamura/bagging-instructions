using System.ComponentModel.DataAnnotations.Schema;

namespace BaggingInstructions.Api.Entities;

[Table("unit")]
public class Unit
{
    [Column("unitid")]
    public long UnitId { get; set; }

    [Column("unitcode")]
    public string? UnitCode { get; set; }

    [Column("unitname")]
    public string? UnitName { get; set; }

    [Column("unitsymbol")]
    public string? UnitSymbol { get; set; }

    [Column("decimalplaces")]
    public int DecimalPlaces { get; set; }

    [Column("isactive")]
    public bool IsActive { get; set; } = true;

    [Column("sortorder")]
    public int SortOrder { get; set; }
}
