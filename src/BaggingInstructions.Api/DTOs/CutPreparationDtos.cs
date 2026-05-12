using System.Text.Json.Serialization;

namespace BaggingInstructions.Api.DTOs;

public class CutPreparationGroupKeyDto
{
    [JsonPropertyName("delvedt")]
    public string? Delvedt { get; set; }

    [JsonPropertyName("manufacturingRouteCode")]
    public string? ManufacturingRouteCode { get; set; }
}

public class CutPreparationGroupDto
{
    [JsonPropertyName("delvedt")]
    public string? Delvedt { get; set; }

    [JsonPropertyName("manufacturingRouteCode")]
    public string? ManufacturingRouteCode { get; set; }

    [JsonPropertyName("manufacturingRouteName")]
    public string? ManufacturingRouteName { get; set; }

    [JsonPropertyName("lineCount")]
    public int LineCount { get; set; }

    [JsonPropertyName("key")]
    public CutPreparationGroupKeyDto Key { get; set; } = new();
}

public class CutPreparationSearchResponseDto
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("groups")]
    public List<CutPreparationGroupDto> Groups { get; set; } = new();
}

public class CutPreparationFilterRequestDto
{
    [JsonPropertyName("delvedt")]
    public string? Delvedt { get; set; }

    [JsonPropertyName("manufacturing_route_codes")]
    public List<string>? ManufacturingRouteCodes { get; set; }

    [JsonPropertyName("itemcd")]
    public string? Itemcd { get; set; }

    [JsonPropertyName("workcenter_ids")]
    public List<long>? WorkcenterIds { get; set; }
}

public class CutPreparationExportRequestDto : CutPreparationFilterRequestDto
{
    [JsonPropertyName("groupKeys")]
    public List<CutPreparationGroupKeyDto> GroupKeys { get; set; } = new();
}

public class CutPreparationProductLabelRequestDto
{
    [JsonPropertyName("groupKeys")]
    public List<CutPreparationGroupKeyDto> GroupKeys { get; set; } = new();

    [JsonPropertyName("delvedt")]
    public string? Delvedt { get; set; }

    [JsonPropertyName("manufacturing_route_codes")]
    public List<string>? ManufacturingRouteCodes { get; set; }

    [JsonPropertyName("itemcd")]
    public string? Itemcd { get; set; }

    [JsonPropertyName("workcenter_ids")]
    public List<long>? WorkcenterIds { get; set; }

    [JsonPropertyName("label_count")]
    public int LabelCount { get; set; } = 1;

    [JsonPropertyName("cut_mode")]
    public bool CutMode { get; set; } = false;

    [JsonPropertyName("instruction_type")]
    public string? InstructionType { get; set; } = "cut";
}
