using BaggingInstructions.Api.Entities;

namespace BaggingInstructions.Api.Services;

/// <summary>個人配送指示書の共通判定・コース/配送順解決。</summary>
public static class PersonalDeliveryHelper
{
    private static readonly string[] DetailRiceItemCodePrefixes = ["3010", "3011", "3111", "3411"];
    public const string TargetCustomerCode = "300";
    public const string StapleFoodItemCodePrefix = "3011";
    public const string SoupItemCodePrefix = "305";

    public static readonly string[] SummaryTargetCustomerCodes = ["300", "310"];

    public enum SummaryItemCategory
    {
        MainDish = 0,
        StapleFood = 1,
        Soup = 2
    }

    public static bool IsRiceItemCode(string? itemCode)
    {
        var code = (itemCode ?? "").Trim();
        if (code.Length < 4) return false;
        return DetailRiceItemCodePrefixes.Contains(code[..4]);
    }

    public static bool IsStapleFoodSummaryItemCode(string? itemCode)
    {
        var code = (itemCode ?? "").Trim();
        return code.Length >= 4 && code[..4] == StapleFoodItemCodePrefix;
    }

    public static bool IsSoupItemCode(string? itemCode)
    {
        var code = (itemCode ?? "").Trim();
        return code.Length >= 3 && code[..3] == SoupItemCodePrefix;
    }

    public static SummaryItemCategory ResolveSummaryItemCategory(string? itemCode)
    {
        if (IsStapleFoodSummaryItemCode(itemCode)) return SummaryItemCategory.StapleFood;
        if (IsSoupItemCode(itemCode)) return SummaryItemCategory.Soup;
        return SummaryItemCategory.MainDish;
    }

    public static int GetSummaryCategorySortOrder(SummaryItemCategory category) => (int)category;

    public static string GetSummaryCategoryLabel(SummaryItemCategory category) => category switch
    {
        SummaryItemCategory.MainDish => "主菜",
        SummaryItemCategory.StapleFood => "主食",
        SummaryItemCategory.Soup => "汁物",
        _ => ""
    };

    public static bool IsTargetCustomer(string? customerCode) =>
        GohanSearchFilter.NormalizeCustomer(customerCode) == TargetCustomerCode;

    public static bool IsSummaryTargetCustomer(string? customerCode)
    {
        var normalized = GohanSearchFilter.NormalizeCustomer(customerCode);
        return SummaryTargetCustomerCodes.Contains(normalized);
    }

    public static bool IsTargetAddinfo08(string? addinfo08) =>
        BentoSearchFilter.IsTargetGohanAddinfo08(addinfo08);

    public static bool IsDetailVariant(string? variant) =>
        !string.Equals((variant ?? "").Trim(), "summary", StringComparison.OrdinalIgnoreCase);

    /// <summary>info19 に応じて customerdeliverylocationaddinfo からコース・配送順を取得する。</summary>
    public static (string Course, string DeliveryOrder) ResolveCourseAndOrder(
        string? info19,
        CustomerDeliveryLocationAddinfo? addinfo)
    {
        if (addinfo == null) return ("", "");
        return (info19 ?? "").Trim() switch
        {
            "1" => (TrimOrEmpty(addinfo.Addinfo01), TrimOrEmpty(addinfo.Addinfo02)),
            "2" => (TrimOrEmpty(addinfo.Addinfo03), TrimOrEmpty(addinfo.Addinfo04)),
            "3" => (TrimOrEmpty(addinfo.Addinfo05), TrimOrEmpty(addinfo.Addinfo06)),
            _ => ("", "")
        };
    }

    public static int CompareDeliveryOrder(string? a, string? b)
    {
        var sa = TrimOrEmpty(a);
        var sb = TrimOrEmpty(b);
        if (int.TryParse(sa, out var na) && int.TryParse(sb, out var nb))
            return na.CompareTo(nb);
        return string.Compare(sa, sb, StringComparison.Ordinal);
    }

    /// <summary>
    /// 配送エリア（コース）の印字用文字列。
    /// addinfo01 / addinfo03 / addinfo05 がスペース区切りの場合は後半（先頭トークン以降）のみ返す。
    /// </summary>
    public static string FormatDeliveryAreaDisplay(string? course)
    {
        var s = (course ?? "").Trim();
        if (s.Length == 0) return "";

        s = s.Replace('\r', ' ').Replace('\n', ' ');
        while (s.Contains("  ", StringComparison.Ordinal))
            s = s.Replace("  ", " ", StringComparison.Ordinal);
        s = s.Trim();

        var parts = s.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length > 1)
            return string.Join(' ', parts.Skip(1));

        return parts[0];
    }

    private static string TrimOrEmpty(string? s) => (s ?? "").Trim();
}
