using System.Text.Json.Serialization;

namespace BaggingInstructions.Api.DTOs;

public sealed class CookingInstructionSearchRowDto
{
    [JsonPropertyName("orderTableId")]
    public long OrderTableId { get; set; }

    /// <summary>品目名（ordertable の item に紐づく）。</summary>
    [JsonPropertyName("itemName")]
    public string ItemName { get; set; } = "";

    /// <summary>表示用 YYYY-MM-DD 形式の納期。</summary>
    [JsonPropertyName("needDate")]
    public string NeedDate { get; set; } = "";

    /// <summary>便表示（slot 名称またはコード）。</summary>
    [JsonPropertyName("slotDisplay")]
    public string SlotDisplay { get; set; } = "";
}

public sealed class CookingInstructionSearchResponseDto
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("rows")]
    public List<CookingInstructionSearchRowDto> Rows { get; set; } = new();
}

/// <summary>作業区マスタ（調理指示書プルダウン用）。</summary>
public sealed class CookingInstructionWorkcenterOptionDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}

/// <summary>便マスタ（調理指示書プルダウン用）。</summary>
public sealed class CookingInstructionSlotOptionDto
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}

/// <summary>調理指示書 PDF 1 行分のモデル。</summary>
public sealed class CookingInstructionPdfLineModel
{
    public string OrderNo { get; set; } = "";
    public string ParentItemCode { get; set; } = "";
    public string ParentItemName { get; set; } = "";
    public string PlannedQuantityDisplay { get; set; } = "";
    public string PlanUnitName { get; set; } = "";

    public string ChildItemCode { get; set; } = "";
    public string ChildItemName { get; set; } = "";
    public string ChildRequiredQtyDisplay { get; set; } = "";
    public string ChildUnitName { get; set; } = "";

    public string NeedDateDisplay { get; set; } = "";
    public string SlotDisplay { get; set; } = "";

    /// <summary>Workplace names used as 作業名 on the report header.</summary>
    public string WorkplaceNames { get; set; } = "";
}
