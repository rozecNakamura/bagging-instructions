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

    /// <summary>得意先コード → 当該得意先への受注数量合計（同一得意先の複数納入場所は合算）。</summary>
    [JsonPropertyName("quantitiesByStore")]
    public Dictionary<string, decimal> QuantitiesByStore { get; set; } = new();
}

public sealed class SortingInquirySearchResponseDto
{
    [JsonPropertyName("storeKeys")]
    public List<string> StoreKeys { get; set; } = new();

    /// <summary>得意先コード → 一覧・Excel 用列見出し（納入場所名称など。複数場所は「／」）。</summary>
    [JsonPropertyName("storeHeaders")]
    public Dictionary<string, string> StoreHeaders { get; set; } = new();

    /// <summary>得意先コード → 納入場所コード（複数は「／」）。仕訳表自動調整 Excel のコード行用。</summary>
    [JsonPropertyName("storeHeaderCodes")]
    public Dictionary<string, string> StoreHeaderCodes { get; set; } = new();

    [JsonPropertyName("rows")]
    public List<SortingInquirySearchRowDto> Rows { get; set; } = new();

    [JsonPropertyName("total")]
    public int Total => Rows.Count;
}
