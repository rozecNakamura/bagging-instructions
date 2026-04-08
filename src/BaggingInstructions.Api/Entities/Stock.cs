using System.ComponentModel.DataAnnotations.Schema;

namespace BaggingInstructions.Api.Entities;

[Table("stock")]
public class Stock
{
    [Column("stockid")]
    public long StockId { get; set; }

    /// <summary>品目コード（item.itemcode へ FK。craftlineax の stock は itemcode で紐づく）。</summary>
    [Column("itemcode")]
    public string? ItemCd { get; set; }

    [Column("warehouseid")]
    public long WarehouseId { get; set; }

    [Column("quantityonhand")]
    public decimal QuantityOnHand { get; set; }

    [Column("quantityavailable")]
    public decimal QuantityAvailable { get; set; }

    [Column("quantityreserved")]
    public decimal QuantityReserved { get; set; }

    [Column("updatedat")]
    public DateTime? UpdatedAt { get; set; }

    public virtual Item? Item { get; set; }
    public virtual Warehouse? Warehouse { get; set; }
}
