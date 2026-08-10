using System.Globalization;
using BaggingInstructions.Api.Core;
using BaggingInstructions.Api.DTOs;
using BaggingInstructions.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace BaggingInstructions.Api.Services;

/// <summary>
/// 納品書画面用：craftlineaxother.cstmeat を喫食日で検索し、
/// craftlineax.customerdeliverylocation と C# で結合して納入場所名を付与する。
/// </summary>
public class DeliveryNoteService
{
    private readonly CstmeatDbContext _cstmeatDb;
    private readonly AppDbContext _appDb;

    public DeliveryNoteService(CstmeatDbContext cstmeatDb, AppDbContext appDb)
    {
        _cstmeatDb = cstmeatDb;
        _appDb = appDb;
    }

    /// <summary>出荷日（info18: YYYYMMDD）で cstmeat を検索し、出荷日・納入場所名を返す。納入場所名は customerdeliverylocation と info02=locationcode で結合。</summary>
    public async Task<List<DeliveryNoteSearchResultDto>> SearchByEatingDateAsync(
        string delvedt,
        string? customerType = null,
        string? deliveryRoute = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(delvedt) || delvedt.Length != 8)
            throw new ArgumentException("出荷日はYYYYMMDD形式（8桁）で指定してください。", nameof(delvedt));

        var customerCodes = GetCustomerCodes(customerType);

        var query = _cstmeatDb.Cstmeats
            .AsNoTracking()
            .Where(c => c.Info18 == delvedt);

        if (customerCodes.Count > 0)
            query = query.Where(c => c.Info01 != null && customerCodes.Contains(c.Info01));

        if (!string.IsNullOrEmpty(deliveryRoute))
            query = query.Where(c => c.Info19 == deliveryRoute);

        var fetched = await query
            .Select(c => new { c.Info01, c.Info02, c.Info18, c.Info19, c.Info07 })
            .ToListAsync(ct);

        // 数量（info07）が0（0食）のレコードは検索対象外
        var rows = fetched
            .Where(c => ParseQuantity(c.Info07) != 0)
            .Select(c => new { c.Info01, c.Info02, c.Info18, Info19 = (c.Info19 ?? "").Trim() })
            .Distinct()
            .ToList();

        if (rows.Count == 0)
            return new List<DeliveryNoteSearchResultDto>();

        var locationCodesFromCstmeat = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in rows)
        {
            var code = (r.Info02 ?? "").Trim();
            if (code.Length > 0) locationCodesFromCstmeat.Add(code);
            var normalized = NormalizeCode(code);
            if (normalized.Length > 0) locationCodesFromCstmeat.Add(normalized);
        }

        // 全納入場所を Customer・Addinfo 込みで取得し、メモリ上でコード一致（Trim・正規化対応）
        var allLocations = await _appDb.CustomerDeliveryLocations
            .AsNoTracking()
            .Include(l => l.Customer)
            .Include(l => l.Addinfo)
            .ToListAsync(ct);
        var locList = allLocations
            .Where(l => l.LocationCode != null && (
                locationCodesFromCstmeat.Contains((l.LocationCode ?? "").Trim()) ||
                locationCodesFromCstmeat.Contains(NormalizeCode(l.LocationCode))))
            .ToList();

        // (CustomerCode, LocationCode) の複数表記で納入場所を引けるようにする（Trim / 大文字小文字 / 先頭ゼロ正規化）
        var locationByKey = new Dictionary<(string, string), CustomerDeliveryLocation>(new KeyComparer());
        var locationByLocCodeOnly = new Dictionary<string, CustomerDeliveryLocation>(StringComparer.OrdinalIgnoreCase);
        foreach (var l in locList)
        {
            var locCode = (l.LocationCode ?? "").Trim();
            var custCode = (l.Customer?.CustomerCode ?? "").Trim();
            if (string.IsNullOrEmpty(locCode)) continue;

            AddKeyVariants(locationByKey, custCode, locCode, l);
            if (!locationByLocCodeOnly.ContainsKey(locCode))
                locationByLocCodeOnly[locCode] = l;
        }

        // 納品便（info19）ごとに customerdeliverylocationaddinfo のコース・配送順で並べ替える
        // 朝(1)=addinfo01/02、昼(2)=addinfo03/04、夜(3)=addinfo05/06。未設定は各便の末尾。
        return rows
            .Select(r =>
            {
                var loc = ResolveLocation(r.Info01, r.Info02, locationByKey, locationByLocCodeOnly);
                if (loc == null) return null;

                var (course, deliveryOrder) = PersonalDeliveryHelper.ResolveCourseAndOrder(r.Info19, loc.Addinfo);
                return new SortableRow
                {
                    Course = course,
                    DeliveryOrder = deliveryOrder,
                    Dto = new DeliveryNoteSearchResultDto
                    {
                        EatingDate = r.Info18,
                        LocationCode = r.Info02,
                        CustomerCode = r.Info01,
                        LocationName = loc.LocationName,
                        DeliveryRoute = r.Info19,
                        DeliveryRouteName = DeliveryRouteDisplay(r.Info19)
                    }
                };
            })
            .Where(x => x != null)
            .Select(x => x!)
            .OrderBy(x => x.Dto.EatingDate)
            .ThenBy(x => GetDeliveryRouteRank(x.Dto.DeliveryRoute))
            .ThenBy(x => x.HasSortKey ? 0 : 1)
            .ThenBy(x => x.Course, DeliveryOrderComparer.Instance)
            .ThenBy(x => x.DeliveryOrder, DeliveryOrderComparer.Instance)
            .ThenBy(x => x.Dto.LocationCode)
            .Select(x => x.Dto)
            .ToList();
    }

    /// <summary>並べ替え用の中間データ（コース・配送順は納品便に応じた addinfo から解決済み）</summary>
    private sealed class SortableRow
    {
        public string Course { get; init; } = "";
        public string DeliveryOrder { get; init; } = "";
        public DeliveryNoteSearchResultDto Dto { get; init; } = new();

        /// <summary>コース・配送順のいずれかが設定されているか（未設定は末尾に回す）</summary>
        public bool HasSortKey => Course.Length > 0 || DeliveryOrder.Length > 0;
    }

    /// <summary>納品便（info19）の並び順。朝→昼→夜、それ以外（未設定含む）は末尾。</summary>
    private static int GetDeliveryRouteRank(string? info19) =>
        (info19 ?? "").Trim() switch
        {
            "1" => 1,
            "2" => 2,
            "3" => 3,
            _ => 9
        };

    /// <summary>納品便（info19）の表示名。画面の納品便プルダウンと同じ表記。</summary>
    private static string DeliveryRouteDisplay(string? info19) =>
        (info19 ?? "").Trim() switch
        {
            "1" => "出荷朝便",
            "2" => "出荷昼便",
            "3" => "出荷夜便",
            var s => s
        };

    private static HashSet<string> GetCustomerCodes(string? customerType) =>
        customerType switch
        {
            "catering" => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "200", "210", "220", "230", "240" },
            "personal" => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "300" },
            "hospital" => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "310" },
            _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        };

    /// <summary>info07（数量）を数値化。数値として解釈できない場合は0扱い。</summary>
    private static decimal ParseQuantity(string? info07) =>
        decimal.TryParse((info07 ?? "").Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0m;

    /// <summary>先頭ゼロを除いたコード（空の場合は "0"）</summary>
    private static string NormalizeCode(string? s)
    {
        var t = (s ?? "").Trim();
        if (t.Length == 0) return "";
        var trimmed = t.TrimStart('0');
        return trimmed.Length == 0 ? "0" : trimmed;
    }

    private static void AddKeyVariants<T>(Dictionary<(string, string), T> dict, string custCode, string locCode, T value)
    {
        var custN = NormalizeCode(custCode);
        var locN = NormalizeCode(locCode);
        var keys = new[] {
            (custCode, locCode),
            (custN, locCode),
            (custCode, locN),
            (custN, locN),
            (custCode.ToUpperInvariant(), locCode),
            (custCode.ToUpperInvariant(), locN),
            (custCode.ToLowerInvariant(), locCode),
            (custCode.ToLowerInvariant(), locN),
        };
        foreach (var key in keys)
        {
            if (string.IsNullOrEmpty(key.Item1) && string.IsNullOrEmpty(key.Item2)) continue;
            if (!dict.ContainsKey(key))
                dict[key] = value;
        }
    }

    /// <summary>cstmeat.info02 と customerdeliverylocation.locationcode で結合し、納入場所を取得。locationname が取れた場合のみ返し、見つからなければ null（結果に含めない）。</summary>
    private static CustomerDeliveryLocation? ResolveLocation(string? info01, string? info02,
        Dictionary<(string, string), CustomerDeliveryLocation> locationByKey,
        Dictionary<string, CustomerDeliveryLocation> locationByLocCodeOnly)
    {
        if (string.IsNullOrEmpty(info02)) return null;
        var c = (info01 ?? "").Trim();
        var l = (info02 ?? "").Trim();
        if (l.Length == 0) return null;

        // 1) info02 = locationcode で結合 → customerdeliverylocation.locationname
        if (locationByLocCodeOnly.TryGetValue(l, out var locByCode) && !string.IsNullOrEmpty(locByCode.LocationName))
            return locByCode;
        if (locationByLocCodeOnly.TryGetValue(NormalizeCode(l), out var locByCodeNorm) && !string.IsNullOrEmpty(locByCodeNorm.LocationName))
            return locByCodeNorm;

        // 2) (得意先, 納入場所) で検索（複数表記）
        var toTry = new[] {
            (c, l),
            (NormalizeCode(c), l),
            (c, NormalizeCode(l)),
            (NormalizeCode(c), NormalizeCode(l)),
            (c.ToUpperInvariant(), l),
            (c.ToUpperInvariant(), NormalizeCode(l)),
            (c.ToLowerInvariant(), l),
            (c.ToLowerInvariant(), NormalizeCode(l)),
        };
        foreach (var key in toTry)
        {
            if (locationByKey.TryGetValue(key, out var loc) && !string.IsNullOrEmpty(loc.LocationName))
                return loc;
        }

        return null;
    }
}

/// <summary>配送順・コースの比較。数値として解釈できる場合は数値比較、できない場合は文字列比較。</summary>
internal sealed class DeliveryOrderComparer : IComparer<string>
{
    public static readonly DeliveryOrderComparer Instance = new();

    public int Compare(string? x, string? y) => PersonalDeliveryHelper.CompareDeliveryOrder(x, y);
}

/// <summary>(string, string) の大文字小文字を無視する比較</summary>
internal sealed class KeyComparer : IEqualityComparer<(string, string)>
{
    public bool Equals((string, string) x, (string, string) y) =>
        StringComparer.OrdinalIgnoreCase.Equals(x.Item1, y.Item1) &&
        StringComparer.OrdinalIgnoreCase.Equals(x.Item2, y.Item2);

    public int GetHashCode((string, string) obj) =>
        HashCode.Combine(
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Item1 ?? ""),
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Item2 ?? ""));
}
