using System.ComponentModel.DataAnnotations.Schema;

namespace BaggingInstructions.Api.Entities;

[Table("classification1")]
public class Classification1
{
    [Column("classification1id")]
    public long Classification1Id { get; set; }

    [Column("classification1code")]
    public string? Classification1Code { get; set; }

    [Column("classification1name")]
    public string? Classification1Name { get; set; }
}
