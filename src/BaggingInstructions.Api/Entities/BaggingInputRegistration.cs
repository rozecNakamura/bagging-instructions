using System.ComponentModel.DataAnnotations.Schema;

namespace BaggingInstructions.Api.Entities;

/// <summary>袋詰投入量登録（製造日・品目単位で1件）。</summary>
[Table("bagginginputregistration")]
public class BaggingInputRegistration
{
    [Column("bagginginputregistrationid")]
    public long BaggingInputRegistrationId { get; set; }

    [Column("product_date")]
    public DateOnly ProductDate { get; set; }

    [Column("item_code")]
    public string ItemCode { get; set; } = "";

    /// <summary>JSON: BaggingInputPayloadDto（lines 配列）。</summary>
    [Column("payload")]
    public string Payload { get; set; } = "{}";

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
}
