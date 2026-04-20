using System.Text.Json.Serialization;

namespace BaggingInstructions.Api.DTOs;

/// <summary>検収の記録簿 店舗（納入場所）マルチセレクト用。</summary>
public sealed class AcceptanceRecordDeliveryLocationOptionDto
{
    [JsonPropertyName("customerCode")]
    public string CustomerCode { get; set; } = "";

    [JsonPropertyName("locationCode")]
    public string LocationCode { get; set; } = "";

    /// <summary>一覧表示用。 <c>locationcode：locationname</c> 形式。</summary>
    [JsonPropertyName("displayLabel")]
    public string DisplayLabel { get; set; } = "";
}

public sealed class AcceptanceRecordSearchRowDto
{
    /// <summary>代表の受注明細 ID（互換・先頭要素）。印刷時は <see cref="SalesOrderLineIds"/> を使用。</summary>
    [JsonPropertyName("salesOrderLineId")]
    public long SalesOrderLineId { get; set; }

    /// <summary>集計に含まれる全 <c>salesorderlineid</c>（印刷 PDF で使用）。</summary>
    [JsonPropertyName("salesOrderLineIds")]
    public List<long> SalesOrderLineIds { get; set; } = new();

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

    /// <summary>納入場所名称（customerdeliverylocation.locationname）。ヘッダー施設名・ページングキー。</summary>
    public string DeliveryLocationName { get; set; } = "";

    /// <summary>品目コード（集計キー・ITEMNUM）。</summary>
    public string ItemCode { get; set; } = "";

    /// <summary>品目名称のみ（ITEMNM）。</summary>
    public string ItemName { get; set; } = "";

    /// <summary>受注明細の出荷日（ページングキー・ヘッダー）。</summary>
    public DateOnly? PlannedShipDate { get; set; }

    /// <summary>受注明細の納品日（ページングキー・ヘッダー）。</summary>
    public DateOnly? PlannedDeliveryDate { get; set; }

    /// <summary>集計用数量（salesorderline.quantity の合算前）。</summary>
    public decimal LineQuantity { get; set; }

    /// <summary><c>salesorderlineaddinfo.addinfo02</c>（食数表示計算用）。</summary>
    public string Addinfo02 { get; set; } = "";

    public string EatDateDisplay { get; set; } = "";
    public string SlotDisplay { get; set; } = "";
    public string ChildItemText { get; set; } = "";
    public string MealCountDisplay { get; set; } = "";
    public string TotalQtyDisplay { get; set; } = "";
    public string UnitName { get; set; } = "";
}
