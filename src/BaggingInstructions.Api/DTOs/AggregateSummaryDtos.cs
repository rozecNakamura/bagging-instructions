using System.Text.Json.Serialization;

namespace BaggingInstructions.Api.DTOs;

public sealed class AggregateSummaryKeyDto
{
    [JsonPropertyName("shipDate")]
    public string ShipDate { get; set; } = "";

    [JsonPropertyName("majorClassificationCode")]
    public string? MajorClassificationCode { get; set; }

    [JsonPropertyName("middleClassificationCode")]
    public string? MiddleClassificationCode { get; set; }
}

public sealed class AggregateSummaryRowDto
{
    [JsonPropertyName("key")]
    public AggregateSummaryKeyDto Key { get; set; } = new();

    [JsonPropertyName("shipDate")]
    public string ShipDate { get; set; } = "";

    [JsonPropertyName("majorClassificationName")]
    public string MajorClassificationName { get; set; } = "";

    [JsonPropertyName("middleClassificationName")]
    public string MiddleClassificationName { get; set; } = "";

    [JsonPropertyName("childItemCount")]
    public int ChildItemCount { get; set; }
}

public sealed class AggregateSummarySearchResponseDto
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("rows")]
    public List<AggregateSummaryRowDto> Rows { get; set; } = new();
}

public sealed class AggregateSummaryReportFilterDto
{
    [JsonPropertyName("from_date")]
    public string? FromDate { get; set; }

    [JsonPropertyName("to_date")]
    public string? ToDate { get; set; }

    [JsonPropertyName("item_code")]
    public string? ItemCode { get; set; }

    [JsonPropertyName("major_class")]
    public string? MajorClass { get; set; }

    [JsonPropertyName("middle_class")]
    public string? MiddleClass { get; set; }
}

public sealed class AggregateSummaryReportRequestDto
{
    [JsonPropertyName("filter")]
    public AggregateSummaryReportFilterDto Filter { get; set; } = new();

    [JsonPropertyName("summaryKeys")]
    public List<AggregateSummaryKeyDto> SummaryKeys { get; set; } = new();
}

public sealed class AggregateSummaryPdfLineModel
{
    public string WarehouseName { get; set; } = "";
    public string ShipDateDisplay { get; set; } = "";
    public string ReportItemName { get; set; } = "";
    public string ChildItemCode { get; set; } = "";
    public string ChildItemName { get; set; } = "";
    public string Quantity { get; set; } = "";
    public string Unit { get; set; } = "";
}

