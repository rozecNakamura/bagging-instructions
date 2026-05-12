namespace BaggingInstructions.Api.QueryResults;

public sealed class CutPreparationGroupSqlRow
{
    public string Delvedt { get; set; } = "";
    public string MfgRouteCode { get; set; } = "";
    public string MfgRouteName { get; set; } = "";
    public int LineCount { get; set; }
}

public sealed class CutPreparationLineIdSqlRow
{
    public long Ordertableid { get; set; }
}
