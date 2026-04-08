using System.Text.Json.Serialization;

namespace BaggingInstructions.Api.DTOs;

/// <summary>検品記録簿 仕入先マルチセレクト用。</summary>
public sealed class InspectionRecordSupplierOptionDto
{
    [JsonPropertyName("supplierCode")]
    public string SupplierCode { get; set; } = "";

    [JsonPropertyName("supplierName")]
    public string SupplierName { get; set; } = "";
}

public sealed class InspectionRecordSearchRowDto
{
    [JsonPropertyName("orderTableId")]
    public long OrderTableId { get; set; }

    /// <summary>注番（ordertableid を文字列で表示）。</summary>
    [JsonPropertyName("orderNo")]
    public string OrderNo { get; set; } = "";

    /// <summary>仕入先表示（コード／名称など）。</summary>
    [JsonPropertyName("supplierDisplay")]
    public string SupplierDisplay { get; set; } = "";

    /// <summary>表示用 YYYY-MM-DD 形式の納期。</summary>
    [JsonPropertyName("needDate")]
    public string NeedDate { get; set; } = "";

    [JsonPropertyName("itemCode")]
    public string ItemCode { get; set; } = "";

    [JsonPropertyName("itemName")]
    public string ItemName { get; set; } = "";

    /// <summary>単位0換算数量の表示文字列。</summary>
    [JsonPropertyName("quantityDisplay")]
    public string QuantityDisplay { get; set; } = "";

    /// <summary>単位0名称。</summary>
    [JsonPropertyName("unitName")]
    public string UnitName { get; set; } = "";
}

public sealed class InspectionRecordSearchResponseDto
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("rows")]
    public List<InspectionRecordSearchRowDto> Rows { get; set; } = new();
}

/// <summary>検品記録簿 PDF 1 行分のモデル。</summary>
public sealed class InspectionRecordPdfLineModel
{
    /// <summary>納品日（納期）表示。</summary>
    public string DeliveryDateDisplay { get; set; } = "";

    /// <summary>注番（ordertableid）。</summary>
    public string OrderNo { get; set; } = "";

    public string ItemCode { get; set; } = "";
    public string ItemName { get; set; } = "";

    /// <summary>規格（itemadditionalinformation.std1→std2→std3 の先頭非空）。</summary>
    public string Spec { get; set; } = "";

    /// <summary>単位0換算数量の表示文字列。</summary>
    public string QuantityDisplay { get; set; } = "";

    /// <summary>単位0名称。</summary>
    public string UnitName { get; set; } = "";

    // 以下の項目は帳票上は空欄とするため、常に空文字を出力する。
    public string DeviationHandling { get; set; } = "";
    public string StorageLocation { get; set; } = "";
    public string DeliveryTime { get; set; } = "";
    public string TemperatureCheck { get; set; } = "";
    public string BestBefore { get; set; } = "";
    public string FreshnessGrade { get; set; } = "";
    public string ExternalAppearance { get; set; } = "";
}

