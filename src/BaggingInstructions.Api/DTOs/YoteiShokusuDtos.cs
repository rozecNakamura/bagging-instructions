using System.Text.Json.Serialization;

namespace BaggingInstructions.Api.DTOs;

/// <summary>予定食数 API レスポンス全体。</summary>
public sealed class YoteiShokusuResponseDto
{
    /// <summary>グループ1（得意先コード200・210）の集計食種列（並び順）。</summary>
    [JsonPropertyName("group1Columns")]
    public List<string> Group1Columns { get; set; } = new();

    /// <summary>グループ2（得意先コード240・300・310）の集計食種列（並び順）。</summary>
    [JsonPropertyName("group2Columns")]
    public List<string> Group2Columns { get; set; } = new();

    /// <summary>グループ1 店舗一覧（並び順）。</summary>
    [JsonPropertyName("group1Stores")]
    public List<YoteiShokusuStoreDto> Group1Stores { get; set; } = new();

    /// <summary>グループ2 店舗一覧（並び順）。</summary>
    [JsonPropertyName("group2Stores")]
    public List<YoteiShokusuStoreDto> Group2Stores { get; set; } = new();
}

/// <summary>予定食数 1 店舗分のデータ。</summary>
public sealed class YoteiShokusuStoreDto
{
    [JsonPropertyName("customerCode")]
    public string CustomerCode { get; set; } = "";

    [JsonPropertyName("locationCode")]
    public string LocationCode { get; set; } = "";

    [JsonPropertyName("locationName")]
    public string LocationName { get; set; } = "";

    /// <summary>基本食種ラベル（m_shisetsu.kihon_shokushu）。例: 「基本」「N」。</summary>
    [JsonPropertyName("kihonShokushu")]
    public string KihonShokushu { get; set; } = "";

    /// <summary>備考（m_shisetsu.remarks）。</summary>
    [JsonPropertyName("remarks")]
    public string Remarks { get; set; } = "";

    [JsonPropertyName("sortOrder")]
    public int SortOrder { get; set; }

    /// <summary>集計行リスト。グループ1は上段4行・下段4行、グループ2は1行。</summary>
    [JsonPropertyName("rows")]
    public List<YoteiShokusuRowDto> Rows { get; set; } = new();
}

/// <summary>予定食数の1集計行。</summary>
public sealed class YoteiShokusuRowDto
{
    /// <summary>上段・下段区分（「通常品」「検食・検体」）。グループ2は空。</summary>
    [JsonPropertyName("sectionLabel")]
    public string SectionLabel { get; set; } = "";

    /// <summary>集計食種名 → 数量。</summary>
    [JsonPropertyName("quantities")]
    public Dictionary<string, decimal> Quantities { get; set; } = new();
}
