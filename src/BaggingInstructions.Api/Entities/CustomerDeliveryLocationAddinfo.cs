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

    [Column("addinfo02")]
    public string? Addinfo02 { get; set; }

    [Column("addinfo03")]
    public string? Addinfo03 { get; set; }

    [Column("addinfo04")]
    public string? Addinfo04 { get; set; }

    [Column("addinfo05")]
    public string? Addinfo05 { get; set; }

    [Column("addinfo06")]
    public string? Addinfo06 { get; set; }

    [Column("addinfo08")]
    public string? Addinfo08 { get; set; }

    [Column("addinfo12")]
    public string? Addinfo12 { get; set; }

    [Column("addinfo13")]
    public string? Addinfo13 { get; set; }

    public virtual CustomerDeliveryLocation CustomerDeliveryLocation { get; set; } = null!;
}
