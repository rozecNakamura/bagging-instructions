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

    /// <summary>紐づく大分類コード（集計表画面などで連動制御に使用）。</summary>
    [JsonPropertyName("majorCode")]
    public string MajorCode { get; set; } = "";
}

public class PreparationWorkFilterRequestDto
{
    [JsonPropertyName("delvedt")]
    public string? Delvedt { get; set; }

    /// <summary>製造便コード（<c>salesorderlineaddinfo.addinfo03</c>）。複数指定時は OR。</summary>
    [JsonPropertyName("manufacturing_route_codes")]
    public List<string>? ManufacturingRouteCodes { get; set; }

    [JsonPropertyName("itemcd")]
    public string? Itemcd { get; set; }

    [JsonPropertyName("majorclassificationid")]
    public long? Majorclassificationid { get; set; }

    [JsonPropertyName("middleclassificationid")]
    public long? Middleclassificationid { get; set; }

    [JsonPropertyName("workcenter_ids")]
    public List<long>? WorkcenterIds { get; set; }

    [JsonPropertyName("warehouse_ids")]
    public List<long>? WarehouseIds { get; set; }
}

/// <summary>作業区マスタ（作業前準備書プルダウン用）。</summary>
public sealed class PreparationWorkWorkcenterOptionDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}

/// <summary>倉庫マスタ（作業前準備書プルダウン用）。親品目 <c>item.warehousecode</c> との突合用。</summary>
public sealed class PreparationWorkWarehouseOptionDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("code")]
    public string Code { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}

/// <summary>製造便（受注明細付帯）。納期当日のオーダに現れるコードの一覧。</summary>
public sealed class PreparationWorkManufacturingRouteOptionDto
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}

public class PreparationWorkExportRequestDto : PreparationWorkFilterRequestDto
{
    [JsonPropertyName("groupKeys")]
    public List<PreparationWorkGroupKeyDto> GroupKeys { get; set; } = new();
}
