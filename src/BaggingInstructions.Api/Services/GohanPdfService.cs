using System.Globalization;

namespace BaggingInstructions.Api.Services;

/// <summary>
/// ご飯盛り付け指示書.rxz 用のタグ値を構築する。
/// BOX: GRAM=食数, PACK=食数×addinfo01。個人: GRAM=addinfo01, PACK=食数。
/// </summary>
public class GohanPdfService
{
    /// <summary>1ページあたりの最大表示行数。</summary>
    public const int RowsPerPage = 23;

    /// <summary>得意先300/310の施設名表示。</summary>
    public const string HomeIndividualLocationLabel = "在宅個人";

    /// <summary>
    /// 印刷行を区分・得意先に応じて集約する。
    /// BOX: addinfo01 ごとに食数合算。個人240: 納入場所×品目×addinfo01。個人300/310: 品目×addinfo01で食数合算。
    /// </summary>
    public static List<GohanPrintRowDto> PreparePrintRows(IEnumerable<GohanPrintRowDto> rows)
    {
        var list = rows.ToList();
        var result = new List<GohanPrintRowDto>();
        result.AddRange(AggregateBoxRows(list.Where(r => IsBox(r.Addinfo08))));
        result.AddRange(AggregateIndividualRows(list.Where(r => IsIndividual(r.Addinfo08))));
        return result
            .OrderBy(r => IsBox(r.Addinfo08) ? 0 : 1)
            .ThenBy(r => r.Itemcd ?? "")
            .ThenBy(r => r.Addinfo01 ?? "")
            .ThenBy(r => r.Shpctrnm ?? "")
            .ToList();
    }

    private static IEnumerable<GohanPrintRowDto> AggregateBoxRows(IEnumerable<GohanPrintRowDto> rows) =>
        rows
            .GroupBy(r => (r.Addinfo01 ?? "").Trim())
            .Select(g =>
            {
                var first = g.First();
                var locationNames = g
                    .Select(x => (x.Shpctrnm ?? "").Trim())
                    .Where(s => s.Length > 0)
                    .Distinct()
                    .ToList();
                return new GohanPrintRowDto
                {
                    Delvedt = first.Delvedt,
                    Addinfo05 = first.Addinfo05,
                    Jobordmernm = first.Jobordmernm,
                    Itemcd = first.Itemcd,
                    Cuscd = first.Cuscd,
                    Addinfo01 = first.Addinfo01,
                    Addinfo08 = first.Addinfo08,
                    Quantity = g.Sum(x => x.Quantity),
                    Shpctrnm = string.Join("、", locationNames)
                };
            });

    private static IEnumerable<GohanPrintRowDto> AggregateIndividualRows(IEnumerable<GohanPrintRowDto> rows)
    {
        var list = rows.ToList();
        var result = new List<GohanPrintRowDto>();

        var rows240 = list.Where(r => GohanSearchFilter.NormalizeCustomer(r.Cuscd) == "240");
        result.AddRange(rows240
            .GroupBy(r => (
                Shpctrcd: (r.Shpctrcd ?? "").Trim(),
                Shpctrnm: (r.Shpctrnm ?? "").Trim(),
                Itemcd: (r.Itemcd ?? "").Trim(),
                Addinfo01: (r.Addinfo01 ?? "").Trim()))
            .Select(g => MergeIndividualGroup(g, keepLocation: true)));

        var rows300310 = list.Where(r => GohanSearchFilter.NormalizeCustomer(r.Cuscd) is "300" or "310");
        result.AddRange(rows300310
            .GroupBy(r => (
                Itemcd: (r.Itemcd ?? "").Trim(),
                Addinfo01: (r.Addinfo01 ?? "").Trim()))
            .Select(g => MergeIndividualGroup(g, keepLocation: false)));

        return result;
    }

    private static GohanPrintRowDto MergeIndividualGroup(
        IEnumerable<GohanPrintRowDto> rows,
        bool keepLocation)
    {
        var list = rows.ToList();
        var first = list[0];
        return new GohanPrintRowDto
        {
            Delvedt = first.Delvedt,
            Addinfo05 = first.Addinfo05,
            Jobordmernm = first.Jobordmernm,
            Itemcd = first.Itemcd,
            Cuscd = first.Cuscd,
            Shpctrcd = keepLocation ? first.Shpctrcd : null,
            Shpctrnm = keepLocation ? first.Shpctrnm : HomeIndividualLocationLabel,
            Addinfo01 = first.Addinfo01,
            Addinfo08 = first.Addinfo08,
            Quantity = list.Sum(x => x.Quantity)
        };
    }

    /// <summary>
    /// 選択行からご飯盛り付け指示書用タグ値を構築する。
    /// </summary>
    public static Dictionary<string, string> BuildTagValues(IReadOnlyList<GohanPrintRowDto> rows)
    {
        var tagValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (rows == null || rows.Count == 0) return tagValues;

        tagValues["Date"] = rows[0].Delvedt ?? "";
        tagValues["Time"] = BaggingEatingTimeLabel.MapFromAddinfo05(rows[0].Addinfo05);
        tagValues["TYPE"] = ResolveType(rows[0].Addinfo08);

        for (int i = 0; i < RowsPerPage; i++)
        {
            var nn = i.ToString("D2");
            var r = i < rows.Count ? rows[i] : null;
            tagValues[$"ITEMNM{nn}"] = r?.Jobordmernm ?? "";
            tagValues[$"LOCATIONNM{nn}"] = r?.Shpctrnm ?? "";
            if (r != null && IsBox(r.Addinfo08))
            {
                tagValues[$"GRAM{nn}"] = r.Quantity.ToString(CultureInfo.InvariantCulture);
                tagValues[$"PACK{nn}"] = FormatQuantityTimesPortion(r.Quantity, r.Addinfo01);
            }
            else
            {
                tagValues[$"GRAM{nn}"] = r?.Addinfo01?.Trim() ?? "";
                tagValues[$"PACK{nn}"] = r != null ? r.Quantity.ToString(CultureInfo.InvariantCulture) : "";
            }
            tagValues[$"UNIT{nn}"] = r != null ? ResolveUnit(r.Addinfo08) : "";
        }

        return tagValues;
    }

    /// <summary>食数 × addinfo01（ご飯量）。</summary>
    public static string FormatQuantityTimesPortion(decimal quantity, string? addinfo01)
    {
        if (!TryParseDecimal(addinfo01, out var portion)) return "";
        return (quantity * portion).ToString(CultureInfo.InvariantCulture);
    }

    public static bool IsBox(string? addinfo08)
    {
        var s = (addinfo08 ?? "").TrimStart();
        return s.StartsWith("0");
    }

    public static bool IsIndividual(string? addinfo08)
    {
        var s = (addinfo08 ?? "").TrimStart();
        return s.StartsWith("1");
    }

    /// <summary>addinfo08 の先頭文字で TYPE を決定する。"0"始まり→"BOX"、"1"始まり→"個人"、その他→空。</summary>
    public static string ResolveType(string? addinfo08)
    {
        if (IsBox(addinfo08)) return "BOX";
        if (IsIndividual(addinfo08)) return "個人";
        return "";
    }

    private static string ResolveUnit(string? addinfo08)
    {
        if (IsBox(addinfo08)) return "食";
        if (IsIndividual(addinfo08)) return "個";
        return "";
    }

    private static bool TryParseDecimal(string? text, out decimal value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        return decimal.TryParse(text.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>
    /// 調理指示書と同様、データ項目に折り返し＋縮小表示を適用する対象フィールドか。
    /// </summary>
    public static bool ShouldApplyTextLayout(string fieldName)
    {
        if (string.IsNullOrEmpty(fieldName)) return false;
        return fieldName.StartsWith("ITEMNM", StringComparison.OrdinalIgnoreCase)
            || fieldName.StartsWith("LOCATIONNM", StringComparison.OrdinalIgnoreCase)
            || fieldName.StartsWith("GRAM", StringComparison.OrdinalIgnoreCase)
            || fieldName.StartsWith("PACK", StringComparison.OrdinalIgnoreCase)
            || fieldName.StartsWith("UNIT", StringComparison.OrdinalIgnoreCase)
            || fieldName.Equals("Date", StringComparison.OrdinalIgnoreCase)
            || fieldName.Equals("Time", StringComparison.OrdinalIgnoreCase)
            || fieldName.Equals("TYPE", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>ご飯盛り付け指示書 PDF 印刷用の1行データ</summary>
public class GohanPrintRowDto
{
    public string? Delvedt { get; set; }
    public string? Jobordmernm { get; set; }
    public string? Itemcd { get; set; }
    public string? Cuscd { get; set; }
    public string? Shpctrcd { get; set; }
    /// <summary>食数（cstmeat 食数）</summary>
    public decimal Quantity { get; set; }
    /// <summary>ご飯量 / 1人あたり分量（SalesOrderLineAddinfo.Addinfo01）</summary>
    public string? Addinfo01 { get; set; }
    /// <summary>TYPE/UNIT判定: CustomerDeliveryLocationAddinfo.Addinfo08</summary>
    public string? Addinfo08 { get; set; }
    /// <summary>Time タグ用: SalesOrderLineAddinfo.Addinfo05（1=朝/2=昼/3=夕）</summary>
    public string? Addinfo05 { get; set; }
    /// <summary>LOCATIONNM タグ用: 納入場所名称</summary>
    public string? Shpctrnm { get; set; }
}
