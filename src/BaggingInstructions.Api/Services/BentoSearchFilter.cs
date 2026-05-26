namespace BaggingInstructions.Api.Services;

/// <summary>弁当箱盛り付け指示書の検索対象判定。</summary>
public static class BentoSearchFilter
{
    public const string TypeOkazu = "okazu";
    public const string TypeGohan = "gohan";

    private static readonly string[] GohanItemCodePrefixes = ["3011", "3111", "3411"];
    private static readonly string[] TargetCustomerCodes = ["240", "300", "310"];

    public static bool IsOkazu(string? bentoType) =>
        !string.Equals((bentoType ?? "").Trim(), TypeGohan, StringComparison.OrdinalIgnoreCase);

    public static bool IsGohan(string? bentoType) =>
        string.Equals((bentoType ?? "").Trim(), TypeGohan, StringComparison.OrdinalIgnoreCase);

    public static bool IsTargetCustomer(string? customerCode)
    {
        var normalized = GohanSearchFilter.NormalizeCustomer(customerCode);
        return TargetCustomerCodes.Contains(normalized);
    }

    public static bool IsTargetGohanItemCode(string? itemCode) =>
        GohanSearchFilter.IsTargetItemCode(itemCode);

    public static bool IsTargetGohanAddinfo08(string? addinfo08)
    {
        var s = (addinfo08 ?? "").TrimStart();
        return s.StartsWith('1');
    }
}
