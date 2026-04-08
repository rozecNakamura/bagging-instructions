using System.ComponentModel.DataAnnotations.Schema;

namespace BaggingInstructions.Api.Entities;

[Table("customeritem")]
public class CustomerItem
{
    /// <summary>主キー。craftlineax では text 列の場合あり。</summary>
    [Column("customeritemid")]
    public string CustomerItemId { get; set; } = "";

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
