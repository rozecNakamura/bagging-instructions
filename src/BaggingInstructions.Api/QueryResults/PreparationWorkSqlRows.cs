namespace BaggingInstructions.Api.QueryResults;

public sealed class PreparationWorkGroupSqlRow
{
    public string Delvedt { get; set; } = "";
    public string MajorCode { get; set; } = "";
    public string MajorName { get; set; } = "";
    public string MiddleCode { get; set; } = "";
    public string MiddleName { get; set; } = "";
    public int LineCount { get; set; }
}

public sealed class PreparationWorkLineIdSqlRow
{
    public long Salesorderlineid { get; set; }
}
