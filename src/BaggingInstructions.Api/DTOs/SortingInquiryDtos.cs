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

    /// <summary>列キー（得意先コード＋区切り＋納入場所コード）→ 当該納入場所列への受注数量合計。</summary>
    [JsonPropertyName("quantitiesByStore")]
    public Dictionary<string, decimal> QuantitiesByStore { get; set; } = new();

    /// <summary>列キー → Σ(単位0換算数量 ÷ addinfo01)（有効な addinfo01 の行のみ）。仕訳表自動調整 Excel の数量セル用。</summary>
    [JsonPropertyName("ratioQuantitiesByStore")]
    public Dictionary<string, decimal> RatioQuantitiesByStore { get; set; } = new();
}

public sealed class SortingInquirySearchResponseDto
{
    [JsonPropertyName("storeKeys")]
    public List<string> StoreKeys { get; set; } = new();

    /// <summary>列キー → 納入場所の表示ラベル（名称優先、無ければコード）。一覧・Excel 4 行目、仕訳表自動調整 Excel 2 行目。</summary>
    [JsonPropertyName("storeHeaders")]
    public Dictionary<string, string> StoreHeaders { get; set; } = new();

    /// <summary>列キー → 納入場所コード。仕分け照会 Excel 1 行目、仕訳表自動調整 Excel 1 行目。</summary>
    [JsonPropertyName("storeHeaderCodes")]
    public Dictionary<string, string> StoreHeaderCodes { get; set; } = new();

    /// <summary>列キー → 得意先コード。仕分け照会 Excel 2 行目。</summary>
    [JsonPropertyName("storeHeaderDeliveryCodes")]
    public Dictionary<string, string> StoreHeaderDeliveryCodes { get; set; } = new();

    /// <summary>列キー → 得意先名（マスタの正式名優先、無ければ略称・コード）。仕分け照会 Excel 3 行目。</summary>
    [JsonPropertyName("storeHeaderDeliveryNames")]
    public Dictionary<string, string> StoreHeaderDeliveryNames { get; set; } = new();

    /// <summary>列キー → Σ(単位0換算数量 ÷ addinfo01)（API 用）。仕訳表自動調整 Excel の 3 行目は明細列の最大値から算出し、この辞書は参照しない。</summary>
    [JsonPropertyName("storeHeaderCapacities")]
    public Dictionary<string, decimal> StoreHeaderCapacities { get; set; } = new();

    [JsonPropertyName("rows")]
    public List<SortingInquirySearchRowDto> Rows { get; set; } = new();

    [JsonPropertyName("total")]
    public int Total => Rows.Count;
}
