using System.Text.Json.Serialization;

namespace BaggingInstructions.Api.DTOs;

/// <summary>検収の記録簿 店舗（納入場所）マルチセレクト用。</summary>
public sealed class AcceptanceRecordDeliveryLocationOptionDto
{
    [JsonPropertyName("customerCode")]
    public string CustomerCode { get; set; } = "";

    [JsonPropertyName("locationCode")]
    public string LocationCode { get; set; } = "";

    /// <summary>一覧表示用（名称・コードなど）。</summary>
    [JsonPropertyName("displayLabel")]
    public string DisplayLabel { get; set; } = "";
}

public sealed class AcceptanceRecordSearchRowDto
{
    [JsonPropertyName("salesOrderLineId")]
    public long SalesOrderLineId { get; set; }

    /// <summary>喫食日（受注納期）表示 YYYY-MM-DD。</summary>
    [JsonPropertyName("eatDate")]
    public string EatDate { get; set; } = "";

    /// <summary>喫食時間（便）。</summary>
    [JsonPropertyName("mealTime")]
    public string MealTime { get; set; } = "";

    /// <summary>子品目（コード・名称）。</summary>
    [JsonPropertyName("childItem")]
    public string ChildItem { get; set; } = "";

    [JsonPropertyName("mealCountDisplay")]
    public string MealCountDisplay { get; set; } = "";

    [JsonPropertyName("totalQtyDisplay")]
    public string TotalQtyDisplay { get; set; } = "";

    [JsonPropertyName("unitName")]
    public string UnitName { get; set; } = "";
}

public sealed class AcceptanceRecordSearchResponseDto
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("rows")]
    public List<AcceptanceRecordSearchRowDto> Rows { get; set; } = new();
}

/// <summary>検収の記録簿 PDF 1 行分。</summary>
public sealed class AcceptanceRecordPdfLineModel
{
    /// <summary>選択順を復元するためのキー（帳票には出力しない）。</summary>
    public long SalesOrderLineId { get; set; }

    public string EatDateDisplay { get; set; } = "";
    public string SlotDisplay { get; set; } = "";
    public string ChildItemText { get; set; } = "";
    public string MealCountDisplay { get; set; } = "";
    public string TotalQtyDisplay { get; set; } = "";
    public string UnitName { get; set; } = "";
}
