using System.Text.Json.Serialization;

namespace BaggingInstructions.Api.DTOs;

/// <summary>
/// 現品票印刷（新）：検索結果1行＝子品目1件。
/// 最上位完成品（親）から BOM を再帰探索して得た子孫品目を、親情報付きで返す。
/// </summary>
public sealed class ProductLabelNewRowDto
{
    /// <summary>合算元の ordertableid 一覧（同一の親品目・子品目で複数オーダあれば複数）。</summary>
    [JsonPropertyName("order_table_ids")]
    public List<long> OrderTableIds { get; set; } = new();

    /// <summary>製造日（YYYYMMDD）</summary>
    [JsonPropertyName("release_date")]
    public string ReleaseDate { get; set; } = "";

    /// <summary>親品目コード（BOM 最上位の完成品）。</summary>
    [JsonPropertyName("parent_item_code")]
    public string ParentItemCode { get; set; } = "";

    [JsonPropertyName("parent_item_name")]
    public string ParentItemName { get; set; } = "";

    /// <summary>子品目コード（孫以下も含む BOM 子孫品目）。</summary>
    [JsonPropertyName("child_item_code")]
    public string ChildItemCode { get; set; } = "";

    [JsonPropertyName("child_item_name")]
    public string ChildItemName { get; set; } = "";

    /// <summary>親（最上位完成品）から見た BOM 階層。1=直接の子、2=孫 …。</summary>
    [JsonPropertyName("depth")]
    public int Depth { get; set; }

    /// <summary>子品目の所要数量（各階層の inputqty/outputqty を累積）。</summary>
    [JsonPropertyName("qty")]
    public decimal Qty { get; set; }

    [JsonPropertyName("unit_name")]
    public string UnitName { get; set; } = "";

    [JsonPropertyName("workcenter_name")]
    public string WorkcenterName { get; set; } = "";

    [JsonPropertyName("slot_code")]
    public string SlotCode { get; set; } = "";

    [JsonPropertyName("slot_name")]
    public string SlotName { get; set; } = "";
}

public sealed class ProductLabelNewSearchResponseDto
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("rows")]
    public List<ProductLabelNewRowDto> Rows { get; set; } = new();
}

/// <summary>現品票印刷（新）：印刷対象1件（画面の1行＝子品目1件）。</summary>
public sealed class ProductLabelNewPrintItemDto
{
    /// <summary>合算元の ordertableid 一覧（画面行の order_table_ids をそのまま送る）。</summary>
    [JsonPropertyName("order_table_ids")]
    public List<long> OrderTableIds { get; set; } = new();

    [JsonPropertyName("child_item_code")]
    public string ChildItemCode { get; set; } = "";

    /// <summary>印刷枚数（1以上）。未指定時は label_count を使用。</summary>
    [JsonPropertyName("count")]
    public int? Count { get; set; }
}

public sealed class ProductLabelNewPrintRequestDto
{
    /// <summary>印刷対象（チェックされた行のみ）。</summary>
    [JsonPropertyName("items")]
    public List<ProductLabelNewPrintItemDto> Items { get; set; } = new();

    /// <summary>既定の印刷枚数（items[].count 未指定時に使用）。</summary>
    [JsonPropertyName("label_count")]
    public int LabelCount { get; set; } = 1;

    /// <summary>ラベルカット方式: "cut_on_item_change" / "no_cut"。SATOプリンタ対応時に使用。</summary>
    [JsonPropertyName("cut_mode")]
    public string CutMode { get; set; } = "no_cut";

    /// <summary>指示書種別: "cut"=50/51/53, "seasoning"=55, "cooking"=50以外。印刷対象の子品目を絞り込む。</summary>
    [JsonPropertyName("instruction_type")]
    public string? InstructionType { get; set; }
}
