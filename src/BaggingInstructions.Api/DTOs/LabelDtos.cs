using System.Text.Json.Serialization;

namespace BaggingInstructions.Api.DTOs;

public class LabelItemDto
{
    [JsonPropertyName("label_type")]
    public string LabelType { get; set; } = "standard"; // "standard" | "irregular"

    [JsonPropertyName("delvedt")]
    public string Delvedt { get; set; } = "";

    [JsonPropertyName("shptm")]
    public string? Shptm { get; set; }

    [JsonPropertyName("itemcd")]
    public string Itemcd { get; set; } = "";

    [JsonPropertyName("itemnm")]
    public string Itemnm { get; set; } = "";

    [JsonPropertyName("expiry_date")]
    public string? ExpiryDate { get; set; }

    [JsonPropertyName("strtemp")]
    public string? Strtemp { get; set; }

    [JsonPropertyName("kikunip")]
    public decimal? Kikunip { get; set; }

    [JsonPropertyName("shpctrnm")]
    public string? Shpctrnm { get; set; }

    [JsonPropertyName("irregular_quantity")]
    public decimal? IrregularQuantity { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; } = 1;
}

public class LabelResponseDto
{
    [JsonPropertyName("items")]
    public List<LabelItemDto> Items { get; set; } = new();
}
