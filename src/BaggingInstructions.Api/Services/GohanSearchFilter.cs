namespace BaggingInstructions.Api.Services;

/// <summary>ご飯盛り付け指示書の検索対象判定。</summary>
public static class GohanSearchFilter
{
    private static readonly string[] ItemCodePrefixes = ["3011", "3111", "3411"];
    private static readonly string[] BoxCustomerCodes = ["200", "210"];
    private static readonly string[] IndividualCustomerCodes = ["240", "300", "310"];
    private static readonly string[] AllCustomerCodes = ["200", "210", "240", "300", "310"];

    /// <summary>品目コード先頭4桁が 3011 / 3111 / 3411 のいずれかか。</summary>
    public static bool IsTargetItemCode(string? itemCode)
    {
        var code = (itemCode ?? "").Trim();
        if (code.Length < 4) return false;
        var prefix = code[..4];
        return ItemCodePrefixes.Contains(prefix);
    }

    /// <summary>区分に応じた対象得意先コード一覧。未指定時は BOX + 個人の全コード。</summary>
    public static IReadOnlyList<string> AllowedCustomerCodes(string? addinfo08Type)
    {
        var type = (addinfo08Type ?? "").Trim();
        return type switch
        {
            "0" => BoxCustomerCodes,
            "1" => IndividualCustomerCodes,
            _ => AllCustomerCodes
        };
    }

    /// <summary>得意先コードが区分に応じた対象か（先頭ゼロ除去後に比較）。</summary>
    public static bool IsTargetCustomer(string? customerCode, string? addinfo08Type)
    {
        var normalized = NormalizeCustomerCode(customerCode);
        if (normalized.Length == 0) return false;
        return AllowedCustomerCodes(addinfo08Type).Contains(normalized);
    }

    private static string NormalizeCustomerCode(string? customerCode)
    {
        var s = (customerCode ?? "").Trim().TrimStart('0');
        return s.Length == 0 ? "0" : s;
    }

    /// <summary>得意先コードを正規化する（先頭ゼロ除去）。</summary>
    public static string NormalizeCustomer(string? customerCode) => NormalizeCustomerCode(customerCode);
}
