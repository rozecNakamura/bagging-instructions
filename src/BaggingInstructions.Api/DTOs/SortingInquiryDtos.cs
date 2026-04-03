using System.Text.Json.Serialization;

namespace BaggingInstructions.Api.DTOs;

public sealed class SortingInquirySearchRowDto
{
    [JsonPropertyName("itemCode")]
    public string ItemCode { get; set; } = "";

    [JsonPropertyName("itemName")]
    public string ItemName { get; set; } = "";

    [JsonPropertyName("foodType")]
    public string FoodType { get; set; } = "";

    /// <summary>店舗キー（customercode|locationcode）→ 受注数量合計。</summary>
    [JsonPropertyName("quantitiesByStore")]
    public Dictionary<string, decimal> QuantitiesByStore { get; set; } = new();
}

public sealed class SortingInquirySearchResponseDto
{
    [JsonPropertyName("storeKeys")]
    public List<string> StoreKeys { get; set; } = new();

    /// <summary>店舗キー → 一覧・Excel 用ヘッダー文言。</summary>
    [JsonPropertyName("storeHeaders")]
    public Dictionary<string, string> StoreHeaders { get; set; } = new();

    [JsonPropertyName("rows")]
    public List<SortingInquirySearchRowDto> Rows { get; set; } = new();

    [JsonPropertyName("total")]
    public int Total => Rows.Count;
}
