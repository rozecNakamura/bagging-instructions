using System.Globalization;

namespace BaggingInstructions.Api.Services;

/// <summary>
/// 弁当箱盛り付け指示書.rxz 用のタグ値を構築する。
/// TYPE=おかず/ご飯。おかず: ITEMNM=食種名称, LOCATIONNM=info17, PACK=食数。
/// ご飯: ITEMNM=品目名, GRAM=1人あたり分量, PACK=食数合計。
/// </summary>
public class BentoPdfService
{
    /// <summary>1ページあたりの最大表示行数。</summary>
    public const int RowsPerPage = 23;

    public const string TypeLabelOkazu = "おかず";
    public const string TypeLabelGohan = "ご飯";

    /// <summary>
    /// 印刷行をおかず/ご飯に応じて集約する。
    /// </summary>
    public static List<BentoPrintRowDto> PreparePrintRows(IEnumerable<BentoPrintRowDto> rows, string? bentoType)
    {
        var list = rows.ToList();
        if (BentoSearchFilter.IsGohan(bentoType))
            return PreparePrintRowsGohan(list);
        return PreparePrintRowsOkazu(list);
    }

    /// <summary>おかず: 喫食日×喫食時間×納入場所×食種×info17 で1行（食数は合算）。</summary>
    public static List<BentoPrintRowDto> PreparePrintRowsOkazu(IEnumerable<BentoPrintRowDto> rows) =>
        rows
            .GroupBy(r => (
                Delvedt: r.Delvedt ?? "",
                Addinfo05: (r.Addinfo05 ?? "").Trim(),
                Shpctrcd: (r.Shpctrcd ?? "").Trim(),
                Info17: (r.Info17 ?? "").Trim(),
                FoodTypeName: (r.FoodTypeName ?? "").Trim()))
            .Select(g =>
            {
                var first = g.First();
                return new BentoPrintRowDto
                {
                    Delvedt = first.Delvedt,
                    Addinfo05 = first.Addinfo05,
                    Shpctrcd = first.Shpctrcd,
                    Shpctrnm = first.Shpctrnm,
                    Quantity = g.Sum(x => x.Quantity),
                    Info17 = first.Info17,
                    FoodTypeName = first.FoodTypeName
                };
            })
            .OrderBy(r => r.FoodTypeName ?? "")
            .ThenBy(r => r.Shpctrnm ?? "")
            .ThenBy(r => r.Info17 ?? "")
            .ToList();

    /// <summary>ご飯: 喫食日×喫食時間×品目コード×1人あたり分量で食数合計。</summary>
    public static List<BentoPrintRowDto> PreparePrintRowsGohan(IEnumerable<BentoPrintRowDto> rows) =>
        rows
            .GroupBy(r => (
                Delvedt: r.Delvedt ?? "",
                Addinfo05: (r.Addinfo05 ?? "").Trim(),
                Itemcd: (r.Itemcd ?? "").Trim(),
                Jobordmernm: (r.Jobordmernm ?? "").Trim(),
                Addinfo01: (r.Addinfo01 ?? "").Trim()))
            .Select(g =>
            {
                var first = g.First();
                return new BentoPrintRowDto
                {
                    Delvedt = first.Delvedt,
                    Addinfo05 = first.Addinfo05,
                    Itemcd = first.Itemcd,
                    Jobordmernm = first.Jobordmernm,
                    Addinfo01 = first.Addinfo01,
                    Quantity = g.Sum(x => x.Quantity)
                };
            })
            .OrderBy(r => r.Itemcd ?? "")
            .ThenBy(r => r.Addinfo01 ?? "")
            .ToList();

    /// <summary>
    /// 選択行から弁当箱盛り付け指示書用タグ値を構築する。
    /// </summary>
    public static Dictionary<string, string> BuildTagValues(IReadOnlyList<BentoPrintRowDto> rows, string? bentoType)
    {
        var tagValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (rows == null || rows.Count == 0) return tagValues;

        tagValues["Date"] = rows[0].Delvedt ?? "";
        tagValues["Time"] = BaggingEatingTimeLabel.MapFromAddinfo05(rows[0].Addinfo05);
        tagValues["TYPE"] = ResolveTypeLabel(bentoType);

        var isGohan = BentoSearchFilter.IsGohan(bentoType);
        for (int i = 0; i < RowsPerPage; i++)
        {
            var nn = i.ToString("D2");
            var r = i < rows.Count ? rows[i] : null;
            if (isGohan)
            {
                tagValues[$"ITEMNM{nn}"] = r?.Jobordmernm ?? "";
                tagValues[$"LOCATIONNM{nn}"] = "";
                tagValues[$"GRAM{nn}"] = r?.Addinfo01?.Trim() ?? "";
                tagValues[$"PACK{nn}"] = r != null
                    ? r.Quantity.ToString(CultureInfo.InvariantCulture)
                    : "";
            }
            else
            {
                tagValues[$"ITEMNM{nn}"] = r?.FoodTypeName ?? "";
                tagValues[$"LOCATIONNM{nn}"] = r?.Info17 ?? "";
                tagValues[$"GRAM{nn}"] = "";
                tagValues[$"PACK{nn}"] = r != null
                    ? r.Quantity.ToString(CultureInfo.InvariantCulture)
                    : "";
            }
        }

        return tagValues;
    }

    public static string ResolveTypeLabel(string? bentoType) =>
        BentoSearchFilter.IsGohan(bentoType) ? TypeLabelGohan : TypeLabelOkazu;

    /// <summary>折り返し＋縮小表示を適用する対象フィールドか。</summary>
    public static bool ShouldApplyTextLayout(string fieldName)
    {
        if (string.IsNullOrEmpty(fieldName)) return false;
        return fieldName.StartsWith("ITEMNM", StringComparison.OrdinalIgnoreCase)
            || fieldName.StartsWith("LOCATIONNM", StringComparison.OrdinalIgnoreCase)
            || fieldName.StartsWith("GRAM", StringComparison.OrdinalIgnoreCase)
            || fieldName.StartsWith("PACK", StringComparison.OrdinalIgnoreCase)
            || fieldName.Equals("Date", StringComparison.OrdinalIgnoreCase)
            || fieldName.Equals("Time", StringComparison.OrdinalIgnoreCase)
            || fieldName.Equals("TYPE", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>弁当箱盛り付け指示書 PDF 印刷用の1行データ</summary>
public class BentoPrintRowDto
{
    public string? Delvedt { get; set; }
    public string? ShptmDisplay { get; set; }
    public string? Jobordmernm { get; set; }
    public string? Itemcd { get; set; }
    public string? Shpctrcd { get; set; }
    public string? Shpctrnm { get; set; }
    public decimal Jobordqun { get; set; }
    /// <summary>食数（cstmeat info07）</summary>
    public decimal Quantity { get; set; }
    /// <summary>1人あたり分量（SalesOrderLineAddinfo.Addinfo01）</summary>
    public string? Addinfo01 { get; set; }
    /// <summary>喫食時間（SalesOrderLineAddinfo.Addinfo05）</summary>
    public string? Addinfo05 { get; set; }
    /// <summary>おかず用: cstmeat.info17</summary>
    public string? Info17 { get; set; }
    /// <summary>おかず用: 食種名称（m_shokushu.shokushu_name、cstmeat.info05↔shokushu_code）</summary>
    public string? FoodTypeName { get; set; }
}
