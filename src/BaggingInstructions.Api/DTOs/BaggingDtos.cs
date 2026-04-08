using System.Text.Json.Serialization;

namespace BaggingInstructions.Api.DTOs;

public class CalculateRequestDto
{
    [JsonPropertyName("jobord_prkeys")]
    public List<long> JobordPrkeys { get; set; } = new();

    [JsonPropertyName("print_type")]
    public string PrintType { get; set; } = "instruction"; // "instruction" | "label"

    /// <summary>登録済み投入量を読み、右上・按分計算に反映する。</summary>
    [JsonPropertyName("use_saved_input")]
    public bool UseSavedInput { get; set; }
}

public class SeasoningAmountDto
{
    [JsonPropertyName("citemcd")]
    public string Citemcd { get; set; } = "";

    [JsonPropertyName("citemgr")]
    public string? Citemgr { get; set; }

    [JsonPropertyName("cfctcd")]
    public string? Cfctcd { get; set; }

    [JsonPropertyName("cdeptcd")]
    public string? Cdeptcd { get; set; }

    [JsonPropertyName("amu")]
    public decimal Amu { get; set; }

    [JsonPropertyName("otp")]
    public decimal Otp { get; set; }

    [JsonPropertyName("calculated_amount")]
    public decimal CalculatedAmount { get; set; }

    [JsonPropertyName("child_item")]
    public ItemDetailDto? ChildItem { get; set; }
}

public class BaggingInstructionItemDto
{
    [JsonPropertyName("shpctrcd")]
    public string? Shpctrcd { get; set; }

    [JsonPropertyName("shpctrnm")]
    public string Shpctrnm { get; set; } = "";

    [JsonPropertyName("itemcd")]
    public string Itemcd { get; set; } = "";

    [JsonPropertyName("itemnm")]
    public string Itemnm { get; set; } = "";

    [JsonPropertyName("delvedt")]
    public string Delvedt { get; set; } = "";

    [JsonPropertyName("shptm")]
    public string? Shptm { get; set; }

    [JsonPropertyName("planned_quantity")]
    public decimal PlannedQuantity { get; set; }

    [JsonPropertyName("adjusted_quantity")]
    public decimal AdjustedQuantity { get; set; }

    /// <summary>在庫・出来高用（受注合算のまま）。</summary>
    [JsonPropertyName("quantity_for_inventory")]
    public decimal QuantityForInventory { get; set; }

    /// <summary>指示書表示用（切上げ後等）。</summary>
    [JsonPropertyName("quantity_for_instruction")]
    public decimal QuantityForInstruction { get; set; }

    [JsonPropertyName("standard_bags")]
    public int StandardBags { get; set; }

    [JsonPropertyName("irregular_quantity")]
    public decimal IrregularQuantity { get; set; }

    [JsonPropertyName("prddt")]
    public string? Prddt { get; set; }

    [JsonPropertyName("current_stock")]
    public decimal CurrentStock { get; set; }

    [JsonPropertyName("seasoning_amounts")]
    public List<SeasoningAmountDto> SeasoningAmounts { get; set; } = new();

    [JsonPropertyName("item")]
    public ItemDetailDto? Item { get; set; }

    [JsonPropertyName("shpctr")]
    public ShpctrDetailDto? Shpctr { get; set; }

    [JsonPropertyName("mboms")]
    public List<MbomDetailDto> Mboms { get; set; } = new();

    [JsonPropertyName("cusmcd")]
    public CusmcdDetailDto? Cusmcd { get; set; }

    [JsonPropertyName("jobordno")]
    public string? Jobordno { get; set; }

    [JsonPropertyName("jobordmernm")]
    public string? Jobordmernm { get; set; }
}

public class BaggingIngredientRowDto
{
    [JsonPropertyName("citemcd")]
    public string Citemcd { get; set; } = "";

    [JsonPropertyName("spec_qty")]
    public decimal? SpecQty { get; set; }

    [JsonPropertyName("total_qty")]
    public decimal? TotalQty { get; set; }

    [JsonPropertyName("unit_name")]
    public string? UnitName { get; set; }
}

public class BaggingInstructionResponseDto
{
    [JsonPropertyName("items")]
    public List<BaggingInstructionItemDto> Items { get; set; } = new();

    /// <summary>袋詰指示書右上テーブル表示用（投入量登録反映時）。</summary>
    [JsonPropertyName("ingredient_display_rows")]
    public List<BaggingIngredientRowDto>? IngredientDisplayRows { get; set; }
}

/// <summary>DB保存用投入量ペイロード（JSON）。</summary>
public class BaggingInputPayloadDto
{
    [JsonPropertyName("lines")]
    public List<BaggingInputLineDto> Lines { get; set; } = new();

    /// <summary>親完成品の出来高（合計）。指定時、登録済み投入量で印刷では施設別受注比で <see cref="BaggingInstructionItemDto.PlannedQuantity"/> を按分してから袋詰計算する。</summary>
    [JsonPropertyName("parent_yield_quantity")]
    public decimal? ParentYieldQuantity { get; set; }
}

public class BaggingInputLineDto
{
    [JsonPropertyName("citemcd")]
    public string Citemcd { get; set; } = "";

    /// <summary>BOM 行順（1 始まり）。袋詰投入量テーブル input_order と対応。</summary>
    [JsonPropertyName("input_order")]
    public int? InputOrder { get; set; }

    [JsonPropertyName("spec_qty")]
    public decimal? SpecQty { get; set; }

    [JsonPropertyName("total_qty")]
    public decimal? TotalQty { get; set; }
}

public class BaggingInputSaveRequestDto
{
    [JsonPropertyName("prddt")]
    public string Prddt { get; set; } = "";

    [JsonPropertyName("itemcd")]
    public string Itemcd { get; set; } = "";

    /// <summary>指定時は craftlineaxother.baggedquantity に保存。省略時は従来 JSON テーブルのみ。</summary>
    [JsonPropertyName("jobord_prkeys")]
    public List<long>? JobordPrkeys { get; set; }

    [JsonPropertyName("payload")]
    public BaggingInputPayloadDto Payload { get; set; } = new();
}

public class BaggingInputGetResponseDto
{
    [JsonPropertyName("prddt")]
    public string Prddt { get; set; } = "";

    [JsonPropertyName("itemcd")]
    public string Itemcd { get; set; } = "";

    [JsonPropertyName("payload")]
    public BaggingInputPayloadDto? Payload { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}

public class BaggingRequiredQuantitiesRequestDto
{
    [JsonPropertyName("jobord_prkeys")]
    public List<long> JobordPrkeys { get; set; } = new();
}

public class BaggingRequiredQuantitiesResponseDto
{
    [JsonPropertyName("total_order_quantity")]
    public decimal TotalOrderQuantity { get; set; }

    [JsonPropertyName("lines")]
    public List<BaggingInputLineDto> Lines { get; set; } = new();
}

public class BaggingCalculateResult
{
    public List<BaggingInstructionItemDto> Items { get; set; } = new();
    public List<BaggingIngredientRowDto>? IngredientDisplayRows { get; set; }
}
