using System.ComponentModel.DataAnnotations.Schema;

namespace BaggingInstructions.Api.Entities;

/// <summary>
/// 納入場所追加情報。craftlineax では <c>deliverylocationid</c> ではなく
/// <c>customercode</c> + <c>deliverylocationcode</c> で <c>customerdeliverylocation</c> と紐づく。
/// </summary>
[Table("customerdeliverylocationaddinfo")]
public class CustomerDeliveryLocationAddinfo
{
    [Column("addinfoid")]
    public long AddinfoId { get; set; }

    [Column("customercode")]
    public string CustomerCode { get; set; } = "";

    /// <summary>DB 列 <c>deliverylocationcode</c>。親 <see cref="CustomerDeliveryLocation.LocationCode"/> と同名プロパティで複合 FK を構成。</summary>
    [Column("deliverylocationcode")]
    public string LocationCode { get; set; } = "";

    [Column("addinfo01")]
    public string? Addinfo01 { get; set; }

    public virtual CustomerDeliveryLocation CustomerDeliveryLocation { get; set; } = null!;
}
