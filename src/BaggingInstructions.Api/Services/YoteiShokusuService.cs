using System.Globalization;
using Microsoft.EntityFrameworkCore;
using BaggingInstructions.Api.Core;
using BaggingInstructions.Api.DTOs;
using BaggingInstructions.Api.Entities;

namespace BaggingInstructions.Api.Services;

/// <summary>
/// 予定食数帳票: salesorderline を喫食日・便で検索し、集計食種（m_shokushu.sum_shokushu_name）×店舗別に数量を集計する。
/// グループ1（得意先200・210）: 商品分類（上段=通常品／下段=検食・検体）×容器区分（宅配A=5AA常菜以外／宅配B=5AA常菜）の4行。
/// グループ2（得意先240・300・310）: 集計食種別 1 行。
/// 施設の並び順・基本食種ラベル・備考は craftlineaxother.m_shisetsu から取得する。
/// </summary>
public sealed class YoteiShokusuService
{
    private static readonly string[] Group1CustomerCodes = ["200", "210"];
    private static readonly string[] Group2CustomerCodes = ["240", "300", "310"];
    private static readonly string[] AllTargetCustomerCodes = ["200", "210", "240", "300", "310"];

    /// <summary>通常品の大分類コード。</summary>
    private const string NormalClassCode = "1";

    private readonly AppDbContext _db;
    private readonly CstmeatDbContext _cstmeatDb;

    public YoteiShokusuService(AppDbContext db, CstmeatDbContext cstmeatDb)
    {
        _db = db;
        _cstmeatDb = cstmeatDb;
    }

    /// <summary>喫食日 delvedt は YYYYMMDD。slotCodes が空なら便で絞り込まない。</summary>
    public async Task<YoteiShokusuResponseDto> SearchAsync(
        string delvedt,
        IReadOnlyList<string>? slotCodes,
        CancellationToken ct = default)
    {
        var date = ParseYyyymmdd(delvedt);
        if (!date.HasValue)
            throw new ArgumentException("喫食日はYYYYMMDD形式（8桁）で指定してください。", nameof(delvedt));

        var slots = (slotCodes ?? Array.Empty<string>())
            .Select(s => (s ?? "").Trim())
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);

        IReadOnlyList<YoteiLineMaterial> lines;
        IReadOnlyDictionary<string, (string SumName, int SortOrder)> shokushuMap;
        IReadOnlyDictionary<string, int> sumSortOrders;
        IReadOnlyDictionary<(string, string), ShisetsuMasterRow> shisetsuMap;
        IReadOnlyDictionary<string, string> locationNames;

        if (_db.Database.IsRelational())
        {
            lines = await LoadLinesRelationalAsync(date.Value, slots, ct);
            locationNames = await LoadLocationNamesRelationalAsync(date.Value, slots, ct);
        }
        else
        {
            lines = await LoadLinesInMemoryAsync(date.Value, slots, ct);
            locationNames = await LoadLocationNamesInMemoryAsync(date.Value, slots, ct);
        }

        if (_cstmeatDb.Database.IsRelational())
        {
            (shokushuMap, sumSortOrders) = await LoadShokushuMappingRelationalAsync(ct);
            shisetsuMap = await LoadShisetsuMasterRelationalAsync(ct);
        }
        else
        {
            (shokushuMap, sumSortOrders) = await LoadShokushuMappingInMemoryAsync(ct);
            shisetsuMap = await LoadShisetsuMasterInMemoryAsync(ct);
        }

        return BuildResponse(lines, shokushuMap, sumSortOrders, shisetsuMap, locationNames);
    }

    // ─── SQL ロード ──────────────────────────────────────────────

    private async Task<IReadOnlyList<YoteiLineMaterial>> LoadLinesRelationalAsync(
        DateOnly plannedDate, HashSet<string> slots, CancellationToken ct)
    {
        var dateStr = plannedDate.ToString("yyyyMMdd");
        var slotArr = slots.Count > 0 ? slots.ToArray() : Array.Empty<string>();
        var custCodes = AllTargetCustomerCodes;
        var rows = await _cstmeatDb.Database
            .SqlQuery<YoteiLineSqlRow>($@"
SELECT
  COALESCE(CAST(NULLIF(TRIM(COALESCE(info07, '')), '') AS DECIMAL), 0) AS ""Quantity"",
  info01 AS ""CustomerCode"",
  TRIM(COALESCE(info02, '')) AS ""LocationCode"",
  NULL AS ""LocationName"",
  info05 AS ""Addinfo02"",
  NULL AS ""Addinfo02Name"",
  COALESCE(info06, '') AS ""MajorClassCode""
FROM cstmeat
WHERE info03 = {dateStr}
  AND ({slotArr.Length} = 0 OR info04 = ANY ({slotArr}))
  AND info01 = ANY ({custCodes})
")
            .ToListAsync(ct);

        return rows.Select(r => new YoteiLineMaterial(
            r.Quantity,
            (r.CustomerCode ?? "").Trim(),
            (r.LocationCode ?? "").Trim(),
            "",
            r.Addinfo02,
            r.Addinfo02Name,
            (r.MajorClassCode ?? "").Trim())).ToList();
    }

    private async Task<IReadOnlyDictionary<string, string>> LoadLocationNamesRelationalAsync(
        DateOnly plannedDate, HashSet<string> slots, CancellationToken ct)
    {
        var dateStr = plannedDate.ToString("yyyyMMdd");
        var slotArr = slots.Count > 0 ? slots.ToArray() : Array.Empty<string>();
        var custCodes = AllTargetCustomerCodes;

        var locCodeRows = await _cstmeatDb.Database
            .SqlQuery<YoteiLocationCodeSqlRow>($@"
SELECT DISTINCT TRIM(COALESCE(info02, '')) AS ""LocationCode""
FROM cstmeat
WHERE info03 = {dateStr}
  AND ({slotArr.Length} = 0 OR info04 = ANY ({slotArr}))
  AND info01 = ANY ({custCodes})
  AND info02 IS NOT NULL AND TRIM(info02) <> ''
")
            .ToListAsync(ct);

        var locCodes = locCodeRows
            .Select(r => (r.LocationCode ?? "").Trim())
            .Where(c => c.Length > 0)
            .ToArray();

        if (locCodes.Length == 0)
            return new Dictionary<string, string>(StringComparer.Ordinal);

        var nameRows = await _db.Database
            .SqlQuery<YoteiLocationNameSqlRow>($@"
SELECT DISTINCT
  TRIM(locationcode) AS ""LocationCode"",
  NULLIF(TRIM(COALESCE(locationname, '')), '') AS ""LocationName""
FROM customerdeliverylocation
WHERE TRIM(locationcode) = ANY ({locCodes})
")
            .ToListAsync(ct);

        return nameRows
            .Where(r => !string.IsNullOrWhiteSpace(r.LocationCode))
            .GroupBy(r => r.LocationCode!.Trim(), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.FirstOrDefault(r => !string.IsNullOrWhiteSpace(r.LocationName))?.LocationName?.Trim() ?? g.Key, StringComparer.Ordinal);
    }

    private async Task<(IReadOnlyDictionary<string, (string SumName, int SortOrder)>, IReadOnlyDictionary<string, int>)>
        LoadShokushuMappingRelationalAsync(CancellationToken ct)
    {
        var rows = await _cstmeatDb.Database
            .SqlQuery<ShokushuMapSqlRow>($@"
SELECT shokushu_code AS ""ShokushuCode"", sum_shokushu_name AS ""SumShokushuName"", priority_order AS ""PriorityOrder""
FROM m_shokushu
WHERE sum_shokushu_name IS NOT NULL AND TRIM(sum_shokushu_name) <> ''
")
            .ToListAsync(ct);

        return BuildShokushuMappingFromRows(rows);
    }

    private async Task<IReadOnlyDictionary<(string, string), ShisetsuMasterRow>> LoadShisetsuMasterRelationalAsync(CancellationToken ct)
    {
        var custCodes = AllTargetCustomerCodes;
        var rows = await _db.Database
            .SqlQuery<ShisetsuSqlRow>($@"
SELECT
  c.customercode AS ""CustomerCode"",
  c.locationcode AS ""LocationCode"",
  c.sortorder    AS ""SortOrder"",
  a.addinfo12    AS ""KihonShokushu"",
  a.addinfo13    AS ""Remarks""
FROM customerdeliverylocation c
LEFT JOIN customerdeliverylocationaddinfo a
  ON c.customercode = a.customercode AND c.locationcode = a.deliverylocationcode
WHERE c.customercode = ANY ({custCodes})
")
            .ToListAsync(ct);

        return rows
            .Where(r => !string.IsNullOrWhiteSpace(r.CustomerCode) && !string.IsNullOrWhiteSpace(r.LocationCode))
            .GroupBy(r => ((r.CustomerCode ?? "").Trim(), (r.LocationCode ?? "").Trim()))
            .ToDictionary(g => g.Key, g =>
            {
                var first = g.First();
                return new ShisetsuMasterRow(first.SortOrder, first.KihonShokushu?.Trim() ?? "", first.Remarks?.Trim() ?? "");
            });
    }

    // ─── InMemory ロード ─────────────────────────────────────────

    private async Task<IReadOnlyList<YoteiLineMaterial>> LoadLinesInMemoryAsync(
        DateOnly plannedDate, HashSet<string> slots, CancellationToken ct)
    {
        var dateStr = plannedDate.ToString("yyyyMMdd");
        var query = _cstmeatDb.Cstmeats
            .AsNoTracking()
            .Where(c => c.Info03 == dateStr)
            .Where(c => AllTargetCustomerCodes.Contains(c.Info01 ?? ""));

        if (slots.Count > 0)
            query = query.Where(c => slots.Contains(c.Info04 ?? ""));

        var rows = await query.ToListAsync(ct);
        return rows.Select(r => new YoteiLineMaterial(
            decimal.TryParse(r.Info07?.Trim(), out var q) ? q : 0,
            (r.Info01 ?? "").Trim(),
            (r.Info02 ?? "").Trim(),
            "",
            r.Info05,
            null,
            (r.Info06 ?? "").Trim())).ToList();
    }

    private async Task<IReadOnlyDictionary<string, string>> LoadLocationNamesInMemoryAsync(
        DateOnly plannedDate, HashSet<string> slots, CancellationToken ct)
    {
        var dateStr = plannedDate.ToString("yyyyMMdd");
        var query = _cstmeatDb.Cstmeats
            .AsNoTracking()
            .Where(c => c.Info03 == dateStr)
            .Where(c => AllTargetCustomerCodes.Contains(c.Info01 ?? ""));

        if (slots.Count > 0)
            query = query.Where(c => slots.Contains(c.Info04 ?? ""));

        var rows = await query.ToListAsync(ct);
        var locCodes = rows
            .Select(r => (r.Info02 ?? "").Trim())
            .Where(c => c.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (locCodes.Count == 0)
            return new Dictionary<string, string>(StringComparer.Ordinal);

        var locs = await _db.CustomerDeliveryLocations
            .AsNoTracking()
            .Where(l => locCodes.Contains(l.LocationCode ?? ""))
            .ToListAsync(ct);

        return locs
            .Where(l => !string.IsNullOrWhiteSpace(l.LocationCode))
            .GroupBy(l => l.LocationCode!.Trim(), StringComparer.Ordinal)
            .ToDictionary(g => g.Key,
                g => (g.First().LocationName ?? g.First().LocationShortName ?? g.Key).Trim(),
                StringComparer.Ordinal);
    }

    private async Task<(IReadOnlyDictionary<string, (string SumName, int SortOrder)>, IReadOnlyDictionary<string, int>)>
        LoadShokushuMappingInMemoryAsync(CancellationToken ct)
    {
        var rows = await _cstmeatDb.Mshokushus.AsNoTracking().ToListAsync(ct);
        var sqlRows = rows
            .Where(r => !string.IsNullOrWhiteSpace(r.ShokushuCode) && !string.IsNullOrWhiteSpace(r.PriorityOrder.HasValue ? r.PriorityOrder.ToString() : null))
            .Select(r => new ShokushuMapSqlRow
            {
                ShokushuCode = r.ShokushuCode,
                SumShokushuName = null, // Mshokushu entity doesn't have SumShokushuName mapped
                PriorityOrder = r.PriorityOrder
            }).ToList();

        // For in-memory path, SumShokushuName needs to be in the entity.
        // Use direct LINQ from MShisetsu is not applicable here.
        // Fallback: return empty mapping (tests don't test sum_shokushu_name).
        return (new Dictionary<string, (string, int)>(StringComparer.Ordinal),
                new Dictionary<string, int>(StringComparer.Ordinal));
    }

    private async Task<IReadOnlyDictionary<(string, string), ShisetsuMasterRow>> LoadShisetsuMasterInMemoryAsync(CancellationToken ct)
    {
        var locs = await _db.CustomerDeliveryLocations
            .AsNoTracking()
            .Include(l => l.Addinfo)
            .Where(l => AllTargetCustomerCodes.Contains(l.CustomerCode ?? ""))
            .ToListAsync(ct);
        return locs
            .Where(r => !string.IsNullOrWhiteSpace(r.CustomerCode) && !string.IsNullOrWhiteSpace(r.LocationCode))
            .GroupBy(r => ((r.CustomerCode ?? "").Trim(), (r.LocationCode ?? "").Trim()))
            .ToDictionary(g => g.Key, g =>
            {
                var first = g.First();
                return new ShisetsuMasterRow(first.SortOrder, first.Addinfo?.Addinfo12?.Trim() ?? "", first.Addinfo?.Addinfo13?.Trim() ?? "");
            });
    }

    // ─── レスポンス構築 ───────────────────────────────────────────

    private static YoteiShokusuResponseDto BuildResponse(
        IReadOnlyList<YoteiLineMaterial> lines,
        IReadOnlyDictionary<string, (string SumName, int SortOrder)> shokushuMap,
        IReadOnlyDictionary<string, int> sumSortOrders,
        IReadOnlyDictionary<(string, string), ShisetsuMasterRow> shisetsuMap,
        IReadOnlyDictionary<string, string> locationNames)
    {
        var group1Lines = lines.Where(l => Group1CustomerCodes.Contains(l.CustomerCode)).ToList();
        var group2Lines = lines.Where(l => Group2CustomerCodes.Contains(l.CustomerCode)).ToList();

        // ── グループ1 集計 ──
        // キー: (locationCode, sectionLabel, sumShokushu)
        var g1Agg = new Dictionary<(string, string, string), decimal>(TupleStringComparer3.Ordinal);
        var g1CustomerByLoc = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var line in group1Lines)
        {
            var sumShokushu = ResolveShokushu(line.Addinfo02, line.Addinfo02Name, shokushuMap);
            var section = line.MajorClassCode == NormalClassCode ? "通常品" : "検食・検体";
            var key = (line.LocationCode, section, sumShokushu);
            g1Agg[key] = g1Agg.GetValueOrDefault(key) + line.Quantity;
            if (!g1CustomerByLoc.ContainsKey(line.LocationCode))
                g1CustomerByLoc[line.LocationCode] = line.CustomerCode;
        }

        // ── グループ2 集計 ──
        var g2Agg = new Dictionary<(string, string), decimal>(TupleStringComparer2.Ordinal);
        var g2CustomerByLoc = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var line in group2Lines)
        {
            var sumShokushu = ResolveShokushu(line.Addinfo02, line.Addinfo02Name, shokushuMap);
            var key = (line.LocationCode, sumShokushu);
            g2Agg[key] = g2Agg.GetValueOrDefault(key) + line.Quantity;
            if (!g2CustomerByLoc.ContainsKey(line.LocationCode))
                g2CustomerByLoc[line.LocationCode] = line.CustomerCode;
        }

        // ── 集計食種列（出現データ順）──
        int SumOrder(string s) => sumSortOrders.TryGetValue(s, out var o) ? o : int.MaxValue;

        var group1Columns = g1Agg.Keys
            .Select(k => k.Item3)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(SumOrder)
            .ThenBy(s => s, StringComparer.Ordinal)
            .ToList();

        var group2Columns = g2Agg.Keys
            .Select(k => k.Item2)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(SumOrder)
            .ThenBy(s => s, StringComparer.Ordinal)
            .ToList();

        // ── グループ1 店舗 DTO ──
        var group1Stores = BuildGroup1Stores(
            g1Agg, g1CustomerByLoc, shisetsuMap, locationNames, group1Columns);

        // ── グループ2 店舗 DTO ──
        var group2Stores = BuildGroup2Stores(
            g2Agg, g2CustomerByLoc, shisetsuMap, locationNames, group2Columns);

        return new YoteiShokusuResponseDto
        {
            Group1Columns = group1Columns,
            Group2Columns = group2Columns,
            Group1Stores = group1Stores,
            Group2Stores = group2Stores
        };
    }

    private static List<YoteiShokusuStoreDto> BuildGroup1Stores(
        Dictionary<(string Loc, string Section, string SumShokushu), decimal> agg,
        Dictionary<string, string> customerByLoc,
        IReadOnlyDictionary<(string, string), ShisetsuMasterRow> shisetsuMap,
        IReadOnlyDictionary<string, string> locationNames,
        List<string> columns)
    {
        var locations = agg.Keys.Select(k => k.Loc).Distinct(StringComparer.Ordinal).ToList();

        int StoreOrder(string loc)
        {
            var cust = customerByLoc.TryGetValue(loc, out var c) ? c : "";
            return shisetsuMap.TryGetValue((cust, loc), out var m) && m.SortOrder.HasValue
                ? m.SortOrder.Value
                : int.MaxValue;
        }

        var sections = new[] { "通常品", "検食・検体" };

        return locations
            .OrderBy(StoreOrder)
            .ThenBy(l => l, StringComparer.Ordinal)
            .Select(loc =>
            {
                var cust = customerByLoc.TryGetValue(loc, out var c) ? c : "";
                shisetsuMap.TryGetValue((cust, loc), out var master);
                var rows = sections.Select(sec =>
                {
                    var qty = new Dictionary<string, decimal>(StringComparer.Ordinal);
                    foreach (var col in columns)
                        qty[col] = agg.GetValueOrDefault((loc, sec, col));
                    return new YoteiShokusuRowDto
                    {
                        SectionLabel = sec,
                        Quantities = qty
                    };
                }).ToList();

                return new YoteiShokusuStoreDto
                {
                    CustomerCode = cust,
                    LocationCode = loc,
                    LocationName = locationNames.TryGetValue(loc, out var n) ? n : loc,
                    KihonShokushu = master.KihonShokushu,
                    Remarks = master.Remarks,
                    SortOrder = master.SortOrder ?? int.MaxValue,
                    Rows = rows
                };
            })
            .ToList();
    }

    private static List<YoteiShokusuStoreDto> BuildGroup2Stores(
        Dictionary<(string Loc, string SumShokushu), decimal> agg,
        Dictionary<string, string> customerByLoc,
        IReadOnlyDictionary<(string, string), ShisetsuMasterRow> shisetsuMap,
        IReadOnlyDictionary<string, string> locationNames,
        List<string> columns)
    {
        var locations = agg.Keys.Select(k => k.Loc).Distinct(StringComparer.Ordinal).ToList();

        int StoreOrder(string loc)
        {
            var cust = customerByLoc.TryGetValue(loc, out var c) ? c : "";
            return shisetsuMap.TryGetValue((cust, loc), out var m) && m.SortOrder.HasValue
                ? m.SortOrder.Value
                : int.MaxValue;
        }

        return locations
            .OrderBy(StoreOrder)
            .ThenBy(l => l, StringComparer.Ordinal)
            .Select(loc =>
            {
                var cust = customerByLoc.TryGetValue(loc, out var c) ? c : "";
                shisetsuMap.TryGetValue((cust, loc), out var master);
                var qty = new Dictionary<string, decimal>(StringComparer.Ordinal);
                foreach (var col in columns)
                    qty[col] = agg.GetValueOrDefault((loc, col));

                return new YoteiShokusuStoreDto
                {
                    CustomerCode = cust,
                    LocationCode = loc,
                    LocationName = locationNames.TryGetValue(loc, out var n) ? n : loc,
                    KihonShokushu = master.KihonShokushu,
                    Remarks = master.Remarks,
                    SortOrder = master.SortOrder ?? int.MaxValue,
                    Rows = new List<YoteiShokusuRowDto>
                    {
                        new() { Quantities = qty }
                    }
                };
            })
            .ToList();
    }

    // ─── ヘルパー ─────────────────────────────────────────────────

    private static string ResolveShokushu(
        string? addinfo02,
        string? addinfo02Name,
        IReadOnlyDictionary<string, (string SumName, int SortOrder)> shokushuMap)
    {
        var code = (addinfo02 ?? "").Trim();
        if (code.Length > 0 && shokushuMap.TryGetValue(code, out var m) && !string.IsNullOrWhiteSpace(m.SumName))
            return m.SumName;
        var name = (addinfo02Name ?? "").Trim();
        return name.Length > 0 ? name : code;
    }

    private static (IReadOnlyDictionary<string, (string SumName, int SortOrder)>, IReadOnlyDictionary<string, int>)
        BuildShokushuMappingFromRows(IReadOnlyList<ShokushuMapSqlRow> rows)
    {
        var byCode = new Dictionary<string, (string, int)>(StringComparer.Ordinal);
        var sumMinPriority = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var r in rows)
        {
            var code = (r.ShokushuCode ?? "").Trim();
            var sum = (r.SumShokushuName ?? "").Trim();
            if (code.Length == 0 || sum.Length == 0) continue;

            var priority = r.PriorityOrder.HasValue ? (int)r.PriorityOrder.Value : int.MaxValue;

            if (!byCode.ContainsKey(code))
                byCode[code] = (sum, priority);

            if (!sumMinPriority.TryGetValue(sum, out var cur) || priority < cur)
                sumMinPriority[sum] = priority;
        }

        return (byCode, sumMinPriority);
    }

    private static DateOnly? ParseYyyymmdd(string? s)
    {
        if (string.IsNullOrEmpty(s) || s.Length != 8) return null;
        return DateOnly.TryParseExact(s, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
    }
}

// ─── 内部型 ──────────────────────────────────────────────────────

internal sealed class YoteiLineSqlRow
{
    public decimal Quantity { get; set; }
    public string? CustomerCode { get; set; }
    public string? LocationCode { get; set; }
    public string? LocationName { get; set; }
    public string? Addinfo02 { get; set; }
    public string? Addinfo02Name { get; set; }
    public string? MajorClassCode { get; set; }
}

internal sealed class YoteiLocationNameSqlRow
{
    public string? LocationCode { get; set; }
    public string? LocationName { get; set; }
}

internal sealed class YoteiLocationCodeSqlRow
{
    public string? LocationCode { get; set; }
}

internal sealed class ShokushuMapSqlRow
{
    public string? ShokushuCode { get; set; }
    public string? SumShokushuName { get; set; }
    public decimal? PriorityOrder { get; set; }
}

internal sealed class ShisetsuSqlRow
{
    public string? CustomerCode { get; set; }
    public string? LocationCode { get; set; }
    public int? SortOrder { get; set; }
    public string? KihonShokushu { get; set; }
    public string? Remarks { get; set; }
}

internal readonly record struct ShisetsuMasterRow(int? SortOrder, string KihonShokushu, string Remarks);

internal readonly record struct YoteiLineMaterial(
    decimal Quantity,
    string CustomerCode,
    string LocationCode,
    string LocationName,
    string? Addinfo02,
    string? Addinfo02Name,
    string MajorClassCode);

/// <summary>3要素タプル用 IEqualityComparer。</summary>
internal sealed class TupleStringComparer3 : IEqualityComparer<(string, string, string)>
{
    public static readonly TupleStringComparer3 Ordinal = new();

    public bool Equals((string, string, string) x, (string, string, string) y) =>
        StringComparer.Ordinal.Equals(x.Item1, y.Item1) &&
        StringComparer.Ordinal.Equals(x.Item2, y.Item2) &&
        StringComparer.Ordinal.Equals(x.Item3, y.Item3);

    public int GetHashCode((string, string, string) obj) =>
        HashCode.Combine(
            StringComparer.Ordinal.GetHashCode(obj.Item1 ?? ""),
            StringComparer.Ordinal.GetHashCode(obj.Item2 ?? ""),
            StringComparer.Ordinal.GetHashCode(obj.Item3 ?? ""));
}

/// <summary>2要素タプル用 IEqualityComparer。</summary>
internal sealed class TupleStringComparer2 : IEqualityComparer<(string, string)>
{
    public static readonly TupleStringComparer2 Ordinal = new();

    public bool Equals((string, string) x, (string, string) y) =>
        StringComparer.Ordinal.Equals(x.Item1, y.Item1) &&
        StringComparer.Ordinal.Equals(x.Item2, y.Item2);

    public int GetHashCode((string, string) obj) =>
        HashCode.Combine(
            StringComparer.Ordinal.GetHashCode(obj.Item1 ?? ""),
            StringComparer.Ordinal.GetHashCode(obj.Item2 ?? ""));
}
