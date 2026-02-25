using System.Text.Json.Serialization;

namespace BaggingInstructions.Api.DTOs;

public class CalculateRequestDto
{
    [JsonPropertyName("jobord_prkeys")]
    public List<long> JobordPrkeys { get; set; } = new();

    [JsonPropertyName("print_type")]
    public string PrintType { get; set; } = "instruction"; // "instruction" | "label"
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

public class BaggingInstructionResponseDto
{
    [JsonPropertyName("items")]
    public List<BaggingInstructionItemDto> Items { get; set; } = new();
}
