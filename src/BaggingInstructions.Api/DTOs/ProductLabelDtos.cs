using System.Text.Json.Serialization;

namespace BaggingInstructions.Api.DTOs;

public class MajorClassificationOptionDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("code")]
    public string Code { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}

public class ProductLabelRowDto
{
    /// <summary>納期（YYYYMMDD）</summary>
    [JsonPropertyName("need_date")]
    public string NeedDate { get; set; } = "";

    /// <summary>品目表示（品目名＋コード）</summary>
    [JsonPropertyName("item_display")]
    public string ItemDisplay { get; set; } = "";

    [JsonPropertyName("qty")]
    public decimal Qty { get; set; }
}

public class ProductLabelSearchResponseDto
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("rows")]
    public List<ProductLabelRowDto> Rows { get; set; } = new();
}
