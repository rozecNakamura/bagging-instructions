using System.Text.Json.Serialization;

namespace BaggingInstructions.Api.DTOs;

public sealed class ProductionInstructionSearchRowDto
{
    [JsonPropertyName("orderTableId")]
    public long OrderTableId { get; set; }

    [JsonPropertyName("itemCode")]
    public string ItemCode { get; set; } = "";

    [JsonPropertyName("itemName")]
    public string ItemName { get; set; } = "";

    /// <summary>表示用数量（ordertable／item 換算ロジックは PDF 親行と同じ）。</summary>
    [JsonPropertyName("quantityDisplay")]
    public string QuantityDisplay { get; set; } = "";

    /// <summary>表示用単位名。</summary>
    [JsonPropertyName("unitName")]
    public string UnitName { get; set; } = "";

    /// <summary>表示用 YYYY-MM-DD 形式の納期。</summary>
    [JsonPropertyName("needDate")]
    public string NeedDate { get; set; } = "";

    /// <summary>便表示（slot 名称またはコード）。</summary>
    [JsonPropertyName("slotDisplay")]
    public string SlotDisplay { get; set; } = "";
}

public sealed class ProductionInstructionSearchResponseDto
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("rows")]
    public List<ProductionInstructionSearchRowDto> Rows { get; set; } = new();
}

/// <summary>作業区マスタ（調味液配合表仕様プルダウン用）。</summary>
public sealed class ProductionInstructionWorkcenterOptionDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}

/// <summary>便マスタ（調味液配合表仕様プルダウン用）。</summary>
public sealed class ProductionInstructionSlotOptionDto
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}

/// <summary>製造指示書 PDF 1 行分のモデル。</summary>
public sealed class ProductionInstructionPdfLineModel
{
    public string OrderNo { get; set; } = "";
    public string ParentItemCode { get; set; } = "";
    public string ParentItemName { get; set; } = "";
    public string PlannedQuantityDisplay { get; set; } = "";
    public string PlanUnitName { get; set; } = "";

    public string ChildItemCode { get; set; } = "";
    public string ChildItemName { get; set; } = "";
    public string ChildSpec { get; set; } = "";
    public string ChildRequiredQtyDisplay { get; set; } = "";
    public string ChildUnitName { get; set; } = "";

    /// <summary>BOM 歩留（yieldpercent）の表示。子行が無い場合は空。</summary>
    public string ChildYieldPercentDisplay { get; set; } = "";

    public string NeedDateDisplay { get; set; } = "";
    public string SlotDisplay { get; set; } = "";
}

