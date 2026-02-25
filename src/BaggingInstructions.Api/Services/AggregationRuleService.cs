namespace BaggingInstructions.Api.Services;

public static class AggregationRuleService
{
    private static readonly Dictionary<string, Dictionary<string, string?>> AggregationRules = new()
    {
        ["000"] = new() { ["morning"] = "by_facility", ["lunch"] = "by_facility", ["dinner"] = "by_facility" },
        ["100"] = new() { ["morning"] = "by_facility", ["lunch"] = "by_facility", ["dinner"] = "by_facility" },
        ["110"] = new() { ["morning"] = "by_facility", ["lunch"] = "by_facility", ["dinner"] = "by_facility" },
        ["120"] = new() { ["morning"] = "by_facility", ["lunch"] = "by_facility", ["dinner"] = "by_facility" },
        ["200"] = new() { ["morning"] = "by_facility", ["lunch"] = "by_catering", ["dinner"] = "by_catering" },
        ["210"] = new() { ["morning"] = "by_facility", ["lunch"] = "by_catering", ["dinner"] = "by_catering" },
        ["240"] = new() { ["morning"] = "by_facility", ["lunch"] = "by_catering", ["dinner"] = "by_catering" },
        ["300"] = new() { ["morning"] = null, ["lunch"] = "by_catering", ["dinner"] = "by_catering" },
        ["310"] = new() { ["morning"] = null, ["lunch"] = "by_catering", ["dinner"] = "by_catering" },
    };

    private static readonly Dictionary<string, string> EatingTimeMap = new()
    {
        ["朝"] = "morning",
        ["昼"] = "lunch",
        ["夕"] = "dinner",
    };

    public static string GetAggregationMethod(string? cuscd, string? shptm)
    {
        var eatingKey = EatingTimeMap.GetValueOrDefault(shptm ?? "", "lunch");
        if (!AggregationRules.TryGetValue(cuscd ?? "", out var rule))
            return "by_facility";
        return rule.GetValueOrDefault(eatingKey, "by_facility") ?? "by_facility";
    }

    /// <summary>集計ルールを適用してグルーピング（キー → そのグループの BaggingDetailRow リスト）</summary>
    public static Dictionary<string, List<BaggingDetailRow>> ApplyAggregationRule(IEnumerable<BaggingDetailRow> rows)
    {
        var grouped = new Dictionary<string, List<BaggingDetailRow>>();
        foreach (var row in rows.OrderBy(r => r.Prkey))
        {
            var method = GetAggregationMethod(row.Cuscd, row.Shptm);
            var key = method == "by_facility"
                ? $"{row.Itemcd}_{row.Shpctrcd}_{row.Shptm}"
                : $"{row.Itemcd}_CATERING_{row.Shptm}";
            if (!grouped.ContainsKey(key))
                grouped[key] = new List<BaggingDetailRow>();
            grouped[key].Add(row);
        }
        return grouped;
    }
}
