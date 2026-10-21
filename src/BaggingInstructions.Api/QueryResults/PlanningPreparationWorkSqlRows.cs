namespace BaggingInstructions.Api.QueryResults;

public sealed class PlanningPreparationWorkIdSqlRow
{
    public long Planningorderid { get; set; }
}

public sealed class PlanningPreparationWorkFinalProductSqlRow
{
    public string Itemcode { get; set; } = "";
    public string Itemname { get; set; } = "";
}

public sealed class PlanningPreparationWorkFinalProductOrderSqlRow
{
    public string Itemcode { get; set; } = "";
    public string Itemname { get; set; } = "";
    public DateOnly? ManufacturingDate { get; set; }
}
