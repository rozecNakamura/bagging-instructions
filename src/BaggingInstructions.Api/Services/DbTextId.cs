using System.Globalization;

namespace BaggingInstructions.Api.Services;

/// <summary>craftlineax で ID 列が text の場合の数値解釈（API は従来どおり long を維持）。</summary>
internal static class DbTextId
{
    public static long ToInt64(string? s, long defaultValue = 0)
    {
        if (string.IsNullOrWhiteSpace(s)) return defaultValue;
        return long.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : defaultValue;
    }

    public static long? ToInt64OrNull(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        return long.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;
    }
}
