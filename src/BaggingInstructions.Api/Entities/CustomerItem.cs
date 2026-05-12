using System.ComponentModel.DataAnnotations.Schema;

namespace BaggingInstructions.Api.Entities;

[Table("customeritem")]
public class CustomerItem
{
    /// <summary>主キー（内部採番）。craftlineax は bigint。</summary>
    [Column("customeritemid")]
    public long CustomerItemId { get; set; }

    /// <summary>品目コード（item.itemcode へ。craftlineax は itemid 列なし）。</summary>
    [Column("itemcode")]
    public string? ItemCd { get; set; }

    [Column("customercode")]
    public string? CustomerCode { get; set; }

    [Column("customername")]
    public string? CustomerName { get; set; }

    [Column("customershortname")]
    public string? CustomerShortName { get; set; }

    [Column("status")]
    public string? Status { get; set; }

    public virtual Customer? Customer { get; set; }
    public virtual Item? Item { get; set; }
}
