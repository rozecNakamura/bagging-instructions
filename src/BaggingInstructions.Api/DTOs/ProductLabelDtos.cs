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
    /// <summary>合算元の ordertableid 一覧（同一品目コードで複数あれば複数）。</summary>
    [JsonPropertyName("order_table_ids")]
    public List<long> OrderTableIds { get; set; } = new();

    /// <summary>製造日（YYYYMMDD）</summary>
    [JsonPropertyName("release_date")]
    public string ReleaseDate { get; set; } = "";

    [JsonPropertyName("item_code")]
    public string ItemCode { get; set; } = "";

    [JsonPropertyName("item_name")]
    public string ItemName { get; set; } = "";

    [JsonPropertyName("qty")]
    public decimal Qty { get; set; }

    [JsonPropertyName("workcenter_name")]
    public string WorkcenterName { get; set; } = "";

    /// <summary>BOM 子品目数（1階層）。0 の場合は子品目未登録。</summary>
    [JsonPropertyName("child_count")]
    public int ChildCount { get; set; }
}

public class ProductLabelPrintRequestDto
{
    [JsonPropertyName("order_table_ids")]
    public List<long> OrderTableIds { get; set; } = new();

    /// <summary>1オーダあたりの印刷枚数（デフォルト1）。per_row_counts が指定された行はそちらを優先する。</summary>
    [JsonPropertyName("label_count")]
    public int LabelCount { get; set; } = 1;

    /// <summary>ラベルカット方式: "cut_on_item_change"（品目切替でカット）/ "no_cut"（連続出力）。SATOプリンタ対応時に使用。</summary>
    [JsonPropertyName("cut_mode")]
    public string CutMode { get; set; } = "no_cut";

    /// <summary>指示書種別: "cut"=50/51, "seasoning"=55, "cooking"=50以外。BOM再帰探索の抽出条件として使用。</summary>
    [JsonPropertyName("instruction_type")]
    public string? InstructionType { get; set; }

    /// <summary>オーダID別の印刷枚数上書き。キーは ordertableid の文字列表現。指定があれば label_count より優先。</summary>
    [JsonPropertyName("per_row_counts")]
    public Dictionary<string, int>? PerRowCounts { get; set; }
}

/// <summary>袋詰め画面など：受注明細（salesorderlineid）から現品票 PDF を出すときのリクエスト。</summary>
public class ProductLabelFromSalesOrderLinesRequestDto
{
    [JsonPropertyName("sales_order_line_ids")]
    public List<long> SalesOrderLineIds { get; set; } = new();

    [JsonPropertyName("label_count")]
    public int LabelCount { get; set; } = 1;

    [JsonPropertyName("cut_mode")]
    public string CutMode { get; set; } = "no_cut";
}

public class ProductLabelSearchResponseDto
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("rows")]
    public List<ProductLabelRowDto> Rows { get; set; } = new();
}
