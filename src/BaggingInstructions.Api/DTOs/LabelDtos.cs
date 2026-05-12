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

    /// <summary>配送便名称（addinfo05name）。ラベルの TIME01 に印字。</summary>
    [JsonPropertyName("shptm_name")]
    public string? ShptmName { get; set; }

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

    /// <summary>分類1名称（classification1.classification1name）。ラベルの CLASSIFICATION1NAME01 に印字。</summary>
    [JsonPropertyName("classification1_name")]
    public string? Classification1Name { get; set; }

    /// <summary>同施設の総ラベル枚数（規格袋数＋端数袋があれば＋1）。PAGENO の分母。</summary>
    [JsonPropertyName("page_no")]
    public int PageNo { get; set; }

    /// <summary>このラベル群の施設内開始ページ番号（1始まり）。PAGENO の分子の起点。</summary>
    [JsonPropertyName("start_page_no")]
    public int StartPageNo { get; set; } = 1;

    [JsonPropertyName("irregular_quantity")]
    public decimal? IrregularQuantity { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; } = 1;

    /// <summary>1袋あたりの充填規格量（親品目の規格除算と同じ値）。</summary>
    [JsonPropertyName("standard_fill_qty")]
    public decimal? StandardFillQty { get; set; }

    /// <summary>品目の単位0名称（item.uni0）。</summary>
    [JsonPropertyName("unit_name")]
    public string? UnitName { get; set; }
}

public class LabelResponseDto
{
    [JsonPropertyName("items")]
    public List<LabelItemDto> Items { get; set; } = new();
}

public class LabelPdfRequestDto
{
    [JsonPropertyName("items")]
    public List<LabelItemDto> Items { get; set; } = new();
}
