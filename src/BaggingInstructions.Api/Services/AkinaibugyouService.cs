using System.Text;
using Microsoft.EntityFrameworkCore;
using BaggingInstructions.Api.Core;
using BaggingInstructions.Api.Entities;

namespace BaggingInstructions.Api.Services;

public sealed record AkinaibugyouFilter(
    string SlipType,
    string DateFrom,
    string TimeFrom,
    string DateTo,
    string TimeTo,
    string? Customer,
    string? Store);

/// <summary>
/// 商奉行出力：cstmeat の範囲検索・テキストエクスポート。
/// LoadCstmeatDetailRowsAsync と同様に CstmeatDbContext を直接使用する
/// （relational: FromSql、non-relational: EF LINQ + in-memory post-filter）。
/// </summary>
public class AkinaibugyouService
{
    private readonly CstmeatDbContext _otherDb;

    public AkinaibugyouService(CstmeatDbContext otherDb)
    {
        _otherDb = otherDb;
    }

    /// <summary>フィルタに合致するレコード件数を返す。</summary>
    public async Task<int> CountAsync(AkinaibugyouFilter filter, CancellationToken ct = default)
    {
        var rows = await QueryRowsAsync(filter, ct);
        return rows.Count;
    }

    /// <summary>
    /// グループ（納品日・得意先・納品時期）ごとにヘッダ1行＋明細行を出力する。
    /// タブ区切り、行末 \r\n、Shift-JIS エンコーディング。
    /// </summary>
    public async Task<byte[]> BuildTextBytesAsync(AkinaibugyouFilter filter, CancellationToken ct = default)
    {
        var sjis = Encoding.GetEncoding("shift_jis");
        var rows = await QueryRowsAsync(filter, ct);

        // 食種マスタ一括取得（info05 = shokushu_code）
        var shokuCodes = rows
            .Select(r => r.Info05?.Trim())
            .OfType<string>()
            .Distinct()
            .ToList();
        var shokushuMap = await _otherDb.Mshokushus
            .Where(m => m.ShokushuCode != null && shokuCodes.Contains(m.ShokushuCode))
            .ToDictionaryAsync(m => m.ShokushuCode!, m => m.ShokushuName ?? "", ct);

        // グループ化: 納品日(info03), 得意先(info02), 納品時期(info04)
        var groups = rows
            .GroupBy(r => (
                Info03: r.Info03?.Trim() ?? "",
                Info02: r.Info02?.Trim() ?? "",
                Info04: r.Info04?.Trim() ?? ""
            ))
            .OrderBy(g => g.Key.Info03)
            .ThenBy(g => g.Key.Info02)
            .ThenBy(g => g.Key.Info04);

        using var ms = new MemoryStream();

        foreach (var group in groups)
        {
            var k = group.Key;

            // ヘッダ行
            WriteRow(ms, sjis, new object[]
            {
                "*",                                  // 区分 (1B 固定)
                "0",                                  // 伝票区分 (1B 固定)
                FormatNouhinDate(k.Info03),           // 納品日 (6B)
                "      ",                             // 請求日 (6B スペース)
                "     0",                             // 伝票番号 (6B 固定)
                Fixed("D" + k.Info02, 13),            // 得意先コード (13B)
                "    ",                               // 担当者コード (4B スペース)
                Fixed(k.Info04 switch { "1" => "朝", "2" => "昼", "3" => "夕", var v => v }, 30), // 摘要 (30B, 納品時期)
                "    ",                               // 審判会社コード (4B スペース)
            });

            // 明細行
            foreach (var r in group)
            {
                var shokuCode = r.Info05?.Trim() ?? "";
                shokushuMap.TryGetValue(shokuCode, out var shokuName);

                WriteRow(ms, sjis, new object[]
                {
                    "0",                                                         // 売上区分 (1B 固定)
                    Fixed("B" + shokuCode + "B", 13),                                  // 商品コード (13B)
                    PadSjisBytes(sjis,                                           // 商品名 (36B, Shift-JIS)
                        (shokuName ?? "") + ":" + FormatMealTime(k.Info04), 36),
                    "   0",                                                       // 倉庫番号 (4B 固定)
                    "         ",                                                  // 注文番号 (9B スペース)
                    "    ",                                                       // 入数 (4B スペース)
                    "   0",                                                       // 箱数 (4B 固定)
                    (r.Info07?.Trim() ?? "0").PadLeft(8),                        // 数量 (8B 右詰め)
                    "    ",                                                       // 単位 (4B スペース)
                    FormatTanka(r.Info08).PadLeft(9),                            // 単価 (9B 右詰め, 小数2桁)
                    "         ",                                                  // 原価 (9B スペース)
                    FormatKingaku(r.Info11).PadLeft(9),                          // 金額 (9B 右詰め, 整数)
                });
            }
        }

        return ms.ToArray();
    }

    /// <summary>フィールド配列を1行分タブ区切りで書き込み、末尾に \r\n を付加する。</summary>
    private static void WriteRow(MemoryStream ms, Encoding sjis, object[] fields)
    {
        for (int i = 0; i < fields.Length; i++)
        {
            if (i > 0) ms.WriteByte(0x09); // タブ
            switch (fields[i])
            {
                case string s:
                    ms.Write(sjis.GetBytes(s));
                    break;
                case byte[] b:
                    ms.Write(b);
                    break;
            }
        }
        ms.WriteByte(0x0D); // CR
        ms.WriteByte(0x0A); // LF
    }

    /// <summary>文字列を n 文字に切り詰め or 右スペース埋めする（ASCII フィールド用）。</summary>
    private static string Fixed(string s, int n) =>
        s.Length >= n ? s[..n] : s.PadRight(n);

    /// <summary>Shift-JIS バイト長が totalBytes になるようスペースで右埋め（超過時は切り詰め）する。</summary>
    private static byte[] PadSjisBytes(Encoding sjis, string text, int totalBytes)
    {
        var bytes = sjis.GetBytes(text);
        if (bytes.Length == totalBytes) return bytes;
        if (bytes.Length > totalBytes) return bytes[..totalBytes];
        var padded = new byte[totalBytes];
        bytes.CopyTo(padded, 0);
        for (int i = bytes.Length; i < totalBytes; i++) padded[i] = 0x20;
        return padded;
    }

    /// <summary>info03 (YYYYMMDD または YYYY-MM-DD) → 西暦年-1988(2桁)+月(2桁,1桁はスペース)+日(2桁) = 6B。</summary>
    private static string FormatNouhinDate(string info03)
    {
        var s = info03.Replace("-", "");
        if (s.Length < 8) return "      ";
        if (!int.TryParse(s[..4], out var year) ||
            !int.TryParse(s[4..6], out var month) ||
            !int.TryParse(s[6..8], out var day))
            return "      ";
        var nen = (year - 1988).ToString("D2");
        var mon = month < 10 ? " " + month : month.ToString();
        return nen + mon + day.ToString("D2");
    }

    /// <summary>info04 ("1"/"2"/"3") を商品名に埋め込む食事時期表記に変換する。</summary>
    private static string FormatMealTime(string info04) =>
        info04 switch
        {
            "1" => "(朝食)",
            "2" => "(昼食)",
            "3" => "(夕食)",
            _   => info04,
        };

    /// <summary>info08 を小数部2桁固定の文字列に変換する。</summary>
    private static string FormatTanka(string? info08)
    {
        if (decimal.TryParse(info08?.Trim(),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var v))
            return v.ToString("0.00");
        return "0.00";
    }

    /// <summary>info11 を整数文字列に変換する。</summary>
    private static string FormatKingaku(string? info11)
    {
        if (decimal.TryParse(info11?.Trim(),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var v))
            return ((long)Math.Round(v)).ToString();
        return "0";
    }

    private async Task<List<Cstmeat>> QueryRowsAsync(AkinaibugyouFilter filter, CancellationToken ct)
    {
        var dateFrom = (filter.DateFrom ?? "").Trim();
        var dateTo   = (filter.DateTo   ?? "").Trim();
        var timeFrom = string.IsNullOrEmpty(filter.TimeFrom) ? "1" : filter.TimeFrom.Trim();
        var timeTo   = string.IsNullOrEmpty(filter.TimeTo)   ? "3" : filter.TimeTo.Trim();
        var fromKey = dateFrom + timeFrom;
        var toKey   = dateTo   + timeTo;

        var info01Filter = filter.SlipType switch
        {
            "sales"    => "300",
            "delivery" => "310",
            _          => null,
        };

        if (_otherDb.Database.IsRelational())
        {
            // relational path: LoadCstmeatDetailRowsAsync と同じ FormattableString 方式
            FormattableString sql = $@"SELECT * FROM cstmeat
WHERE (COALESCE(info03, '') || COALESCE(info04, '')) >= {fromKey}
  AND (COALESCE(info03, '') || COALESCE(info04, '')) <= {toKey}";

            IQueryable<Cstmeat> q = _otherDb.Cstmeats.FromSql(sql).AsNoTracking();

            if (info01Filter != null)
                q = q.Where(c => (c.Info01 ?? "").Trim() == info01Filter);
            if (!string.IsNullOrWhiteSpace(filter.Customer))
            {
                var cust = filter.Customer.Trim();
                q = q.Where(c => (c.Info01 ?? "").Trim() == cust);
            }
            if (!string.IsNullOrWhiteSpace(filter.Store))
            {
                var store = filter.Store.Trim();
                q = q.Where(c => (c.Info02 ?? "").Trim() == store);
            }

            return await q.ToListAsync(ct);
        }

        // non-relational path（テスト用 in-memory DB）
        var all = await _otherDb.Cstmeats.AsNoTracking().ToListAsync(ct);
        return all.Where(r =>
        {
            var key = (r.Info03 ?? "").Trim() + (r.Info04 ?? "").Trim();
            if (string.CompareOrdinal(key, fromKey) < 0) return false;
            if (string.CompareOrdinal(key, toKey)   > 0) return false;
            if (info01Filter != null &&
                (r.Info01 ?? "").Trim() != info01Filter) return false;
            if (!string.IsNullOrWhiteSpace(filter.Customer) &&
                (r.Info01 ?? "").Trim() != filter.Customer.Trim()) return false;
            if (!string.IsNullOrWhiteSpace(filter.Store) &&
                (r.Info02 ?? "").Trim() != filter.Store.Trim()) return false;
            return true;
        }).ToList();
    }
}
