using BaggingInstructions.Api.Core;
using BaggingInstructions.Api.DTOs;
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

    /// <summary>喫食日（info03: YYYYMMDD）で cstmeat を検索し、喫食日・納入場所名を返す。納入場所名は customerdeliverylocation と info02=locationcode で結合。</summary>
    public async Task<List<DeliveryNoteSearchResultDto>> SearchByEatingDateAsync(string delvedt, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(delvedt) || delvedt.Length != 8)
            throw new ArgumentException("喫食日はYYYYMMDD形式（8桁）で指定してください。", nameof(delvedt));

        var rows = await _cstmeatDb.Cstmeats
            .AsNoTracking()
            .Where(c => c.Info03 == delvedt)
            .Select(c => new { c.Info01, c.Info02, c.Info03 })
            .Distinct()
            .ToListAsync(ct);

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

        // 全納入場所を Customer 込みで取得し、メモリ上でコード一致（Trim・正規化対応）
        var allLocations = await _appDb.CustomerDeliveryLocations
            .AsNoTracking()
            .Include(l => l.Customer)
            .ToListAsync(ct);
        var locList = allLocations
            .Where(l => l.LocationCode != null && (
                locationCodesFromCstmeat.Contains((l.LocationCode ?? "").Trim()) ||
                locationCodesFromCstmeat.Contains(NormalizeCode(l.LocationCode))))
            .ToList();

        // (CustomerCode, LocationCode) の複数表記で LocationName を引けるようにする（Trim / 大文字小文字 / 先頭ゼロ正規化）
        var locationNameByKey = new Dictionary<(string, string), string>(new KeyComparer());
        var locationNameByLocCodeOnly = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var l in locList)
        {
            var locCode = (l.LocationCode ?? "").Trim();
            var custCode = (l.Customer?.CustomerCode ?? "").Trim();
            var name = l.LocationName ?? "";
            if (string.IsNullOrEmpty(locCode)) continue;

            AddKeyVariants(locationNameByKey, custCode, locCode, name);
            if (!locationNameByLocCodeOnly.ContainsKey(locCode))
                locationNameByLocCodeOnly[locCode] = name;
        }

        return rows
            .Select(r => new DeliveryNoteSearchResultDto
            {
                EatingDate = r.Info03,
                LocationCode = r.Info02,
                CustomerCode = r.Info01,
                LocationName = GetLocationName(r.Info01, r.Info02, locationNameByKey, locationNameByLocCodeOnly)
            })
            .Where(x => !string.IsNullOrEmpty(x.LocationName))
            .OrderBy(x => x.EatingDate)
            .ThenBy(x => x.LocationCode)
            .ToList();
    }

    /// <summary>先頭ゼロを除いたコード（空の場合は "0"）</summary>
    private static string NormalizeCode(string? s)
    {
        var t = (s ?? "").Trim();
        if (t.Length == 0) return "";
        var trimmed = t.TrimStart('0');
        return trimmed.Length == 0 ? "0" : trimmed;
    }

    private static void AddKeyVariants(Dictionary<(string, string), string> dict, string custCode, string locCode, string name)
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
                dict[key] = name;
        }
    }

    /// <summary>cstmeat.info02 と customerdeliverylocation.locationcode で結合し、locationname を取得。対応する納入場所が見つかった場合のみ名前を返し、見つからなければ null（結果に含めない）。</summary>
    private static string? GetLocationName(string? info01, string? info02,
        Dictionary<(string, string), string> locationNameByKey,
        Dictionary<string, string> locationNameByLocCodeOnly)
    {
        if (string.IsNullOrEmpty(info02)) return null;
        var c = (info01 ?? "").Trim();
        var l = (info02 ?? "").Trim();
        if (l.Length == 0) return null;

        // 1) info02 = locationcode で結合 → customerdeliverylocation.locationname
        if (locationNameByLocCodeOnly.TryGetValue(l, out var nameByCode) && !string.IsNullOrEmpty(nameByCode))
            return nameByCode;
        if (locationNameByLocCodeOnly.TryGetValue(NormalizeCode(l), out var nameByCodeNorm) && !string.IsNullOrEmpty(nameByCodeNorm))
            return nameByCodeNorm;

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
            if (locationNameByKey.TryGetValue(key, out var name) && !string.IsNullOrEmpty(name))
                return name;
        }

        return null;
    }
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
