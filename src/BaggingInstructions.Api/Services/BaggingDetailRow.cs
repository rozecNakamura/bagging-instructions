using BaggingInstructions.Api.DTOs;

namespace BaggingInstructions.Api.Services;

/// <summary>袋詰め計算用の1行（新DBの SalesOrderLine 由来）。API の prkey は salesorderlineid。</summary>
public class BaggingDetailRow
{
    public long Prkey { get; set; }
    public string? Prddt { get; set; }
    public string? Delvedt { get; set; }
    public string? Shptm { get; set; }
    public string? Cuscd { get; set; }
    public string? Shpctrcd { get; set; }
    public string? Itemcd { get; set; }
    public decimal Jobordqun { get; set; }
    public string? Jobordmernm { get; set; }
    public string? Jobordno { get; set; }
    public long? ItemId { get; set; }
    public string? Shpctrnm { get; set; }
    /// <summary>端数処理の除数（std 優先、未設定時 car0）</summary>
    public decimal Divisor { get; set; } = 1;
    /// <summary>規格袋数計算用（car0）</summary>
    public decimal Car0 { get; set; } = 1;
    public List<SeasoningBomRow> SeasoningBoms { get; set; } = new();
    public ItemDetailDto? Item { get; set; }
    public ShpctrDetailDto? Shpctr { get; set; }
    public CusmcdDetailDto? Cusmcd { get; set; }
    public List<MbomDetailDto> Mboms { get; set; } = new();
}

/// <summary>調味液計算用の BOM 1行</summary>
public class SeasoningBomRow
{
    public decimal Otp { get; set; }
    public decimal Amu { get; set; }
    public string? ChildItemCd { get; set; }
    public string? ChildUnitName { get; set; }
}
