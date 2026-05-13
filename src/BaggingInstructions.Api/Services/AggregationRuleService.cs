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

    /// <summary>
    /// salesorderlineaddinfo の配送便コード・名称（addinfo04 / addinfo04name）から集計用の喫食キーを決定する。
    /// </summary>
    public static string ResolveMealPeriodKey(string? deliverySlotCode, string? deliverySlotName)
    {
        var name = deliverySlotName ?? "";
        foreach (var kv in EatingTimeMap.OrderByDescending(x => x.Key.Length))
        {
            if (name.Contains(kv.Key, StringComparison.Ordinal))
                return kv.Value;
        }

        var code = (deliverySlotCode ?? "").Trim();
        if (code.Length >= 2 && int.TryParse(code.AsSpan(0, 2), out var hour))
        {
            if (hour < 10) return "morning";
            if (hour < 15) return "lunch";
            return "dinner";
        }

        if (EatingTimeMap.TryGetValue(code, out var mapped))
            return mapped;

        return "lunch";
    }

    public static string GetAggregationMethod(string? cuscd, string? shptm, string? shptmName = null)
    {
        var eatingKey = ResolveMealPeriodKey(shptm, shptmName);
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
            var method = GetAggregationMethod(row.Cuscd, row.Shptm, row.ShptmName);
            var dv = row.Delvedt ?? "";
            var addinfo05 = row.Addinfo05 ?? "";
            var key = method == "by_facility"
                ? $"{row.Itemcd}_{row.Shpctrcd}_{row.Shptm}_{dv}_{addinfo05}"
                : $"{row.Itemcd}_CATERING_{row.Shptm}_{dv}_{addinfo05}";
            if (!grouped.ContainsKey(key))
                grouped[key] = new List<BaggingDetailRow>();
            grouped[key].Add(row);
        }
        return grouped;
    }
}
