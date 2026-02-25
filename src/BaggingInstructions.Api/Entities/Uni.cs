using System.ComponentModel.DataAnnotations.Schema;

namespace BaggingInstructions.Api.Entities.Legacy;

[Table("uni")]
public class Uni
{
    [Column("prkey")]
    public long Prkey { get; set; }

    [Column("unicd")]
    public string? Unicd { get; set; } = "";

    [Column("uninm")]
    public string? Uninm { get; set; } = "";

    [Column("deldt")]
    public DateTime? Deldt { get; set; }

    [Column("ludate")]
    public DateTime? Ludate { get; set; }

    [Column("uuser")]
    public string? Uuser { get; set; } = "";

    [Column("udate")]
    public DateTime? Udate { get; set; }

    [Column("dispno")]
    public decimal Dispno { get; set; }

    [Column("uniinfnm")]
    public string? Uniinfnm { get; set; } = "";
}
