namespace BaggingInstructions.Api.QueryResults;

public sealed class PlanningPreparationWorkIdSqlRow
{
    public long Planningorderid { get; set; }
    public string Delvedt { get; set; } = "";
    public string MajorCode { get; set; } = "";
    public string MiddleCode { get; set; } = "";
}

public sealed class PlanningPreparationWorkFinalProductSqlRow
{
    public string Itemcode { get; set; } = "";
    public string Itemname { get; set; } = "";
}

/// <summary>最終製品の候補となる計画オーダー（productno 単位で取得）。</summary>
public sealed class PlanningPreparationWorkProductNoOrderSqlRow
{
    public string Itemcode { get; set; } = "";
    public string ProductNo { get; set; } = "";
    public DateOnly? ManufacturingDate { get; set; }
}

/// <summary>親品目から BOM を遡った品目と、親品目からの最長の段数。</summary>
public sealed class PlanningPreparationWorkBomAncestorSqlRow
{
    public string Itemcode { get; set; } = "";
    public string Itemname { get; set; } = "";
    public int Depth { get; set; }
}
