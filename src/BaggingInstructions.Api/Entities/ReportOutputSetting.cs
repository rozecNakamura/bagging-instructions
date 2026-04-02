using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BaggingInstructions.Api.Entities;

/// <summary>
/// Optional DB-driven display names for reports. If the table is missing, services fall back to template / defaults.
/// </summary>
[Table("reportoutputsetting")]
public class ReportOutputSetting
{
    [Key]
    [Column("reportcode")]
    public string ReportCode { get; set; } = "";

    [Column("displayname")]
    public string DisplayName { get; set; } = "";
}
