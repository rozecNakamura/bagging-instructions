using System.ComponentModel.DataAnnotations.Schema;

namespace BaggingInstructions.Api.Entities;

/// <summary>
/// 仕入先マスタ。ordertable.suppliercode と結合する。
/// </summary>
[Table("supplier")]
public class Supplier
{
    [Column("suppliercode")]
    public string SupplierCode { get; set; } = "";

    [Column("suppliername")]
    public string? SupplierName { get; set; }
}
