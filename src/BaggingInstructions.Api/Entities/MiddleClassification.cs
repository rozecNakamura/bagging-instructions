using System.ComponentModel.DataAnnotations.Schema;

namespace BaggingInstructions.Api.Entities;

[Table("middleclassification")]
public class MiddleClassification
{
    [Column("middleclassificationid")]
    public long MiddleClassificationId { get; set; }

    [Column("middleclassificationcode")]
    public string? MiddleClassificationCode { get; set; }

    [Column("middleclassificationname")]
    public string? MiddleClassificationName { get; set; }

    [Column("majorclassificationcode")]
    public string? MajorClassificationCode { get; set; }
}
