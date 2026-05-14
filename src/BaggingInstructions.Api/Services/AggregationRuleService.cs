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

    /// <summary>addinfo05（1=朝, 2=昼, 3=夜）から集計用の喫食キーを決定する。</summary>
    private static string ResolveMealPeriodKeyFromAddinfo05(string? addinfo05)
    {
        var s = (addinfo05 ?? "").Trim()
            .Replace('１', '1')
            .Replace('２', '2')
            .Replace('３', '3');
        return s switch
        {
            "1" => "morning",
            "2" => "lunch",
            "3" => "dinner",
            _ => "lunch"
        };
    }

    public static string GetAggregationMethod(string? cuscd, string? addinfo05)
    {
        var eatingKey = ResolveMealPeriodKeyFromAddinfo05(addinfo05);
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
            var method = GetAggregationMethod(row.Cuscd, row.Addinfo05);
            var dv = row.Delvedt ?? "";
            var addinfo05 = row.Addinfo05 ?? "";
            var key = method == "by_facility"
                ? $"{row.Itemcd}_{row.Shpctrcd}_{dv}_{addinfo05}"
                : $"{row.Itemcd}_CATERING_{dv}_{addinfo05}";
            if (!grouped.ContainsKey(key))
                grouped[key] = new List<BaggingDetailRow>();
            grouped[key].Add(row);
        }
        return grouped;
    }
}
