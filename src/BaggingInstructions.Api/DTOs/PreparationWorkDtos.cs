using System.Text.Json.Serialization;

namespace BaggingInstructions.Api.DTOs;

public class PreparationWorkGroupKeyDto
{
    [JsonPropertyName("delvedt")]
    public string Delvedt { get; set; } = "";

    [JsonPropertyName("majorClassificationCode")]
    public string? MajorClassificationCode { get; set; }

    [JsonPropertyName("middleClassificationCode")]
    public string? MiddleClassificationCode { get; set; }
}

public class PreparationWorkGroupDto
{
    [JsonPropertyName("key")]
    public PreparationWorkGroupKeyDto Key { get; set; } = new();

    [JsonPropertyName("delvedt")]
    public string Delvedt { get; set; } = "";

    [JsonPropertyName("majorClassificationName")]
    public string MajorClassificationName { get; set; } = "";

    [JsonPropertyName("middleClassificationName")]
    public string MiddleClassificationName { get; set; } = "";

    [JsonPropertyName("lineCount")]
    public int LineCount { get; set; }
}

public class PreparationWorkSearchResponseDto
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("groups")]
    public List<PreparationWorkGroupDto> Groups { get; set; } = new();
}

public class MiddleClassificationOptionDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("code")]
    public string Code { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}

public class PreparationWorkFilterRequestDto
{
    [JsonPropertyName("delvedt")]
    public string? Delvedt { get; set; }

    [JsonPropertyName("slot")]
    public string? Slot { get; set; }

    [JsonPropertyName("itemcd")]
    public string? Itemcd { get; set; }

    [JsonPropertyName("majorclassificationid")]
    public long? Majorclassificationid { get; set; }

    [JsonPropertyName("middleclassificationid")]
    public long? Middleclassificationid { get; set; }
}

public class PreparationWorkExportRequestDto : PreparationWorkFilterRequestDto
{
    [JsonPropertyName("groupKeys")]
    public List<PreparationWorkGroupKeyDto> GroupKeys { get; set; } = new();
}
