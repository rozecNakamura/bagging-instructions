using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BaggingInstructions.Api.Entities;

/// <summary>craftlineaxother.baggedquantity — 袋詰投入量（製造日・親品目単位で行を置換）。</summary>
[Table("baggedquantity")]
public class BaggedQuantity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("baggedquantityid")]
    public long BaggedQuantityId { get; set; }

    [Column("productdate")]
    public DateOnly ProductDate { get; set; }

    [Column("parentitemcode")]
    public string ParentItemCode { get; set; } = "";

    [Column("childitemcode")]
    public string ChildItemCode { get; set; } = "";

    [Column("inputorder")]
    public int InputOrder { get; set; }

    [Column("standardquantity")]
    public decimal? StandardQuantity { get; set; }

    [Column("totalquantity")]
    public decimal? TotalQuantity { get; set; }

    [Column("isprinted")]
    public bool IsPrinted { get; set; }

    [Column("isinstructionprinted")]
    public bool IsInstructionPrinted { get; set; }

    [Column("islabelprinted")]
    public bool IsLabelPrinted { get; set; }

    [Column("updatedat")]
    public DateTime UpdatedAt { get; set; }
}
