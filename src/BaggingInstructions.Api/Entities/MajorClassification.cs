using System.ComponentModel.DataAnnotations.Schema;

namespace BaggingInstructions.Api.Entities;

[Table("majorclassification")]
public class MajorClassification
{
    [Column("majorclassificationid")]
    public long MajorClassificationId { get; set; }

    [Column("majorclassificationcode")]
    public string? MajorClassificationCode { get; set; }

    [Column("majorclassificationname")]
    public string? MajorClassificationName { get; set; }
}
