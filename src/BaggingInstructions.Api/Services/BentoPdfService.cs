using System.Globalization;

namespace BaggingInstructions.Api.Services;

/// <summary>
/// 弁当箱盛り付け指示書（ご飯）.rxz 用のタグ値を構築する。
/// PACK＝salesorderline.quantity / salesorderlineaddinfo.addinfo01（1人あたり分量）, GRAM＝ordertable.qty（受注数量）, LOCATIONNM＝なし。
/// </summary>
public class BentoPdfService
{
    /// <summary>1ページあたりの最大表示行数。</summary>
    public const int RowsPerPage = 23;

    /// <summary>
    /// 選択行から弁当箱盛り付け指示書用タグ値を構築する。
    /// Date=喫食日, Time=配送便表示, ITEMNM=品目名, LOCATIONNM=なし, GRAM=qty, PACK=quantity/addinfo01。
    /// </summary>
    public static Dictionary<string, string> BuildTagValues(IReadOnlyList<BentoPrintRowDto> rows)
    {
        var tagValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (rows == null || rows.Count == 0) return tagValues;

        tagValues["Date"] = rows[0].Delvedt ?? "";
        tagValues["Time"] = rows[0].ShptmDisplay ?? "";

        for (int i = 0; i < RowsPerPage; i++)
        {
            var nn = i.ToString("D2");
            var r = i < rows.Count ? rows[i] : null;
            tagValues[$"ITEMNM{nn}"] = r?.Jobordmernm ?? "";
            tagValues[$"LOCATIONNM{nn}"] = ""; // なし
            // GRAM＝ordertable.qty（受注数量）。InvariantCulture で小数点表記を統一
            tagValues[$"GRAM{nn}"] = r != null ? r.Jobordqun.ToString(CultureInfo.InvariantCulture) : "";
            // PACK＝salesorderline.quantity / salesorderlineaddinfo.addinfo01（1人あたり分量）
            tagValues[$"PACK{nn}"] = r != null ? FormatMealCountDisplay(r.Quantity, r.Addinfo01) : "";
        }

        return tagValues;
    }

    /// <summary>quantity ÷ addinfo01（1人あたり分量）。除数が無効な場合は空文字。</summary>
    public static string FormatMealCountDisplay(decimal quantity, string? perCapitaPortionText)
    {
        var divisor = ParseDivisor(perCapitaPortionText);
        if (!divisor.HasValue || divisor.Value == 0) return "";
        var value = quantity / divisor.Value;
        return value.ToString(CultureInfo.InvariantCulture);
    }

    private static decimal? ParseDivisor(string? perCapitaPortionText)
    {
        if (string.IsNullOrWhiteSpace(perCapitaPortionText)) return null;
        if (decimal.TryParse(perCapitaPortionText.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) && v != 0)
            return v;
        return null;
    }
}

/// <summary>弁当箱盛り付け指示書（ご飯）PDF 印刷用の1行データ</summary>
public class BentoPrintRowDto
{
    public string? Delvedt { get; set; }
    public string? ShptmDisplay { get; set; }
    public string? Jobordmernm { get; set; }
    public decimal Jobordqun { get; set; }
    /// <summary>GRAM 計算用: SalesOrderLine.Quantity</summary>
    public decimal Quantity { get; set; }
    /// <summary>PACK 計算用: SalesOrderLineAddinfo.Addinfo01（1人あたり分量）</summary>
    public string? Addinfo01 { get; set; }
    /// <summary>ページ分割キー: CustomerDeliveryLocationAddinfo.Addinfo08（先頭 "0"/"1" でページを分ける）</summary>
    public string? Addinfo08 { get; set; }
}
