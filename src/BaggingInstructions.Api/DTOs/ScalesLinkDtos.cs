namespace BaggingInstructions.Api.DTOs;

public sealed class ScalesLinkOrderRowDto
{
    public long Ordertableid { get; init; }
    public string Itemcode { get; init; } = "";
    public string Addinfo06 { get; init; } = "";
    public DateOnly? Releasedate { get; init; }
    public string? Workcentercode { get; init; }
    public decimal Qty { get; init; }
}

public sealed class ScalesLinkOrdersResponseDto
{
    public int TotalCount { get; init; }
    public List<ScalesLinkOrderRowDto> Orders { get; init; } = new();
}
