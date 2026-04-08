namespace BaggingInstructions.Api.Services;

public static class ItemCodeKind
{
    /// <summary>液体品目：品目コードが 55 で始まる。</summary>
    public static bool IsLiquid(string? itemcd) =>
        !string.IsNullOrEmpty(itemcd) && itemcd.StartsWith("55", StringComparison.Ordinal);
}
