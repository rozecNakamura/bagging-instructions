using System.ComponentModel.DataAnnotations.Schema;

namespace BaggingInstructions.Api.Entities;

[Table("minorclassification")]
public class MinorClassification
{
    [Column("minorclassificationid")]
    public long MinorClassificationId { get; set; }

    [Column("minorclassificationcode")]
    public string? MinorClassificationCode { get; set; }

    [Column("minorclassificationname")]
    public string? MinorClassificationName { get; set; }

    [Column("majorclassificationcode")]
    public string? MajorClassificationCode { get; set; }

    [Column("middleclassificationcode")]
    public string? MiddleClassificationCode { get; set; }
}
