using System.Globalization;

namespace BaggingInstructions.Api.Services;

/// <summary>
/// 作業前準備書の帳票・CSV 明細の並び順。
/// 帳票は改頁判定に合わせ、日付 → 職場コード → 製造便コード → 分類コード → 殺菌温度 → 注番号 → 親品目コード → 子品目コード。
/// </summary>
internal static class PreparationWorkReportSort
{
    public static List<PreparationPdfLineModel> SortPdfLines(IReadOnlyList<PreparationPdfLineModel> lines)
    {
        return lines
            .OrderBy(l => l.DateDisplay ?? "", StringComparer.Ordinal)
            .ThenBy(l => l.WorkplaceCode ?? "", StringComparer.Ordinal)
            .ThenBy(l => l.ManufacturingRouteCode ?? "", StringComparer.Ordinal)
            .ThenBy(l => l.MiddleClassificationCode ?? "", StringComparer.Ordinal)
            .ThenBy(l => TemperatureKind(l.TemperatureRange))
            .ThenBy(l => TemperatureNumericPart(l.TemperatureRange))
            .ThenBy(l => l.TemperatureRange ?? "", StringComparer.Ordinal)
            .ThenBy(l => OrderNoSortKey(l.OrderNo))
            .ThenBy(l => l.ParentItemcode ?? "", StringComparer.Ordinal)
            .ThenBy(l => l.ChildItemcode ?? "", StringComparer.Ordinal)
            .ToList();
    }

    public static List<PreparationCsvRow> SortCsvRows(IReadOnlyList<PreparationCsvRow> rows)
    {
        return rows
            .OrderBy(r => r.DeliveryDate ?? "", StringComparer.Ordinal)
            .ThenBy(r => r.WorkplaceName ?? "", StringComparer.Ordinal)
            .ThenBy(r => TemperatureKind(r.SterilizationTemperatureRange))
            .ThenBy(r => TemperatureNumericPart(r.SterilizationTemperatureRange))
            .ThenBy(r => r.SterilizationTemperatureRange ?? "", StringComparer.Ordinal)
            .ThenBy(r => OrderNoSortKey(r.OrderNo))
            .ThenBy(r => r.ParentItemcode ?? "", StringComparer.Ordinal)
            .ThenBy(r => r.ChildItemcode ?? "", StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>0 = 数値として解釈可能、1 = 空以外の非数値、2 = 空（最後）。</summary>
    private static int TemperatureKind(string? s)
    {
        var t = s?.Trim() ?? "";
        if (t.Length == 0) return 2;
        return decimal.TryParse(t, NumberStyles.Any, CultureInfo.InvariantCulture, out _) ? 0 : 1;
    }

    private static decimal TemperatureNumericPart(string? s)
    {
        var t = s?.Trim() ?? "";
        return decimal.TryParse(t, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0m;
    }

    private static long OrderNoSortKey(string? s)
    {
        var t = s?.Trim() ?? "";
        if (t.Length == 0) return long.MaxValue;
        return long.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : long.MaxValue - 1;
    }
}
