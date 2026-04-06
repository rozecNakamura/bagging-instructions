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

    /// <summary>得意先コード → Σ(単位0換算数量 ÷ addinfo01)（有効な addinfo01 の行のみ）。仕訳表自動調整 Excel の数量セル用。</summary>
    [JsonPropertyName("ratioQuantitiesByStore")]
    public Dictionary<string, decimal> RatioQuantitiesByStore { get; set; } = new();
}

public sealed class SortingInquirySearchResponseDto
{
    [JsonPropertyName("storeKeys")]
    public List<string> StoreKeys { get; set; } = new();

    /// <summary>得意先コード → 一覧・Excel 用列見出し（<c>customer</c> マスタの得意先名。略称優先）。</summary>
    [JsonPropertyName("storeHeaders")]
    public Dictionary<string, string> StoreHeaders { get; set; } = new();

    /// <summary>得意先コード → 得意先コード（仕訳表自動調整 Excel 1 行目）。</summary>
    [JsonPropertyName("storeHeaderCodes")]
    public Dictionary<string, string> StoreHeaderCodes { get; set; } = new();

    /// <summary>得意先コード → 納入場所コード（複数は「／」）。仕訳表自動調整 Excel 2 行目。</summary>
    [JsonPropertyName("storeHeaderDeliveryCodes")]
    public Dictionary<string, string> StoreHeaderDeliveryCodes { get; set; } = new();

    /// <summary>得意先コード → 納入場所表示名（名称優先、無ければコード。複数は「／」）。仕訳表自動調整 Excel 3 行目。</summary>
    [JsonPropertyName("storeHeaderDeliveryNames")]
    public Dictionary<string, string> StoreHeaderDeliveryNames { get; set; } = new();

    /// <summary>得意先コード → 列ごとの最大収容相当（当該検索の受注明細について Σ(単位0換算数量 ÷ addinfo01)）。仕訳表自動調整 Excel 3 行目。addinfo01 は文字列から数値パース。</summary>
    [JsonPropertyName("storeHeaderCapacities")]
    public Dictionary<string, decimal> StoreHeaderCapacities { get; set; } = new();

    [JsonPropertyName("rows")]
    public List<SortingInquirySearchRowDto> Rows { get; set; } = new();

    [JsonPropertyName("total")]
    public int Total => Rows.Count;
}
