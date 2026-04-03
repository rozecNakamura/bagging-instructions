using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using BaggingInstructions.Api.Core;
using BaggingInstructions.Api.DTOs;
using BaggingInstructions.Api.Entities;

namespace BaggingInstructions.Api.Services;

/// <summary>
/// 仕分け照会: salesorderline を喫食日（planneddeliverydate）・便（slotcode）で検索し、
/// 品目×食種×店舗別に数量を集計する。食種は <see cref="SalesOrderLineAddinfo.Addinfo02Name"/>（無ければ <see cref="SalesOrderLineAddinfo.Addinfo02"/>）。
/// </summary>
public sealed class SortingInquiryService
{
    private readonly AppDbContext _db;

    public SortingInquiryService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<ProductionInstructionSlotOptionDto>> ListSlotsAsync(CancellationToken ct = default)
    {
        var rows = await _db.Database
            .SqlQuery<ProductionInstructionSlotSqlRow>($@"
SELECT COALESCE(slotcode, '') AS ""Code"", COALESCE(slotname, '') AS ""Name""
FROM deliveryslot
ORDER BY slotcode
")
            .ToListAsync(ct);

        return rows
            .Where(r => !string.IsNullOrWhiteSpace(r.Code))
            .GroupBy(r => r.Code, StringComparer.Ordinal)
            .Select(g => g.First())
            .OrderBy(r => r.Code, StringComparer.Ordinal)
            .Select(r => new ProductionInstructionSlotOptionDto
            {
                Code = r.Code ?? "",
                Name = r.Name ?? ""
            })
            .ToList();
    }

    /// <summary>喫食日 delvedt は YYYYMMDD。slotCodes が空なら便で絞り込まない。</summary>
    public async Task<SortingInquirySearchResponseDto> SearchAsync(
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

        IReadOnlyList<SortingInquiryLineMaterial> materials;
        if (_db.Database.IsRelational())
            materials = await LoadLinesForSearchRelationalAsync(date.Value, slots, ct);
        else
            materials = await LoadLinesForSearchInMemoryAsync(date.Value, slots, ct);

        return BuildSearchResponse(materials);
    }

    /// <summary>
    /// PostgreSQL では ID 列が bigint / text で混在し得るため、集計に必要な列だけを SQL で取得し bigint/text いずれも読み取りエラーにしない。
    /// </summary>
    private async Task<IReadOnlyList<SortingInquiryLineMaterial>> LoadLinesForSearchRelationalAsync(
        DateOnly plannedDate,
        HashSet<string> slots,
        CancellationToken ct)
    {
        var slotArr = slots.Count > 0 ? slots.ToArray() : Array.Empty<string>();
        var rows = await _db.Database
            .SqlQuery<SortingInquiryLineSqlRow>($@"
SELECT
  s.quantity AS ""Quantity"",
  s0.customercode AS ""CustomerCode"",
  c.locationcode AS ""LocationCode"",
  c.locationname AS ""LocationName"",
  COALESCE(i.itemcode, '') AS ""ItemCode"",
  COALESCE(i.itemname, '') AS ""ItemName"",
  s1.addinfo02 AS ""Addinfo02"",
  s1.addinfo02name AS ""Addinfo02Name""
FROM salesorderline s
LEFT JOIN item i ON s.itemcode = i.itemcode
INNER JOIN salesorder s0 ON s.salesorderid = s0.salesorderid
LEFT JOIN customerdeliverylocation c
  ON s0.customercode = c.customercode AND s0.customerdeliverylocationcode = c.locationcode
LEFT JOIN salesorderlineaddinfo s1 ON s.salesorderlineid = s1.salesorderlineid
WHERE s.planneddeliverydate = {plannedDate}
  AND ({slotArr.Length} = 0 OR COALESCE(s.slotcode, '') = ANY ({slotArr}))
ORDER BY COALESCE(i.itemcode, ''), s.salesorderlineid
")
            .ToListAsync(ct);

        return rows.Select(r => new SortingInquiryLineMaterial(
            r.Quantity,
            (r.CustomerCode ?? "").Trim(),
            (r.LocationCode ?? "").Trim(),
            (r.LocationName ?? "").Trim(),
            (r.ItemCode ?? "").Trim(),
            (r.ItemName ?? "").Trim(),
            r.Addinfo02,
            r.Addinfo02Name)).ToList();
    }

    private async Task<IReadOnlyList<SortingInquiryLineMaterial>> LoadLinesForSearchInMemoryAsync(
        DateOnly plannedDate,
        HashSet<string> slots,
        CancellationToken ct)
    {
        var query = _db.SalesOrderLines
            .AsNoTracking()
            .Include(l => l.SalesOrder!)
                .ThenInclude(so => so!.CustomerDeliveryLocation)
            .Include(l => l.Item)
            .Include(l => l.Addinfo)
            .Where(l => l.PlannedDeliveryDate == plannedDate);

        if (slots.Count > 0)
            query = query.Where(l => l.SlotCode != null && slots.Contains(l.SlotCode));

        var lines = await query
            .OrderBy(l => l.Item!.ItemCd ?? "")
            .ThenBy(l => l.SalesOrderLineId)
            .ToListAsync(ct);

        return lines.Select(l => new SortingInquiryLineMaterial(
            l.Quantity,
            (l.SalesOrder?.CustomerCode ?? "").Trim(),
            (l.SalesOrder?.CustomerDeliveryLocation?.LocationCode ?? "").Trim(),
            (l.SalesOrder?.CustomerDeliveryLocation?.LocationName ?? "").Trim(),
            (l.Item?.ItemCd ?? "").Trim(),
            (l.Item?.ItemName ?? "").Trim(),
            l.Addinfo?.Addinfo02,
            l.Addinfo?.Addinfo02Name)).ToList();
    }

    private static SortingInquirySearchResponseDto BuildSearchResponse(IReadOnlyList<SortingInquiryLineMaterial> lines)
    {
        if (lines.Count == 0)
            return new SortingInquirySearchResponseDto();

        var firstLineByStoreKey = new Dictionary<string, (string LocCode, string LocName)>(StringComparer.Ordinal);
        foreach (var line in lines)
        {
            var cust = line.CustomerCode;
            var locCode = line.LocationCode;
            var locName = line.LocationName;
            if (string.IsNullOrEmpty(locCode) && string.IsNullOrEmpty(locName))
                continue;
            var key = StoreKey(cust, locCode);
            if (!firstLineByStoreKey.ContainsKey(key))
                firstLineByStoreKey[key] = (locCode, locName);
        }

        var displayLabelCount = firstLineByStoreKey.Values
            .Select(v => string.IsNullOrEmpty(v.LocName) ? v.LocCode : v.LocName)
            .GroupBy(l => l, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        var storeHeaders = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var kv in firstLineByStoreKey)
        {
            var (locCode, locName) = kv.Value;
            var baseH = string.IsNullOrEmpty(locName) ? locCode : locName;
            var customerFromKey = CustomerCodeFromStoreKey(kv.Key);
            if (displayLabelCount.GetValueOrDefault(baseH) > 1 && !string.IsNullOrEmpty(locCode))
            {
                // 同じ表示名が複数キーにあるときは 納入場所コードで区別（名称のみの重複は「コード＋名称」）
                storeHeaders[kv.Key] = string.IsNullOrEmpty(locName)
                    ? (string.IsNullOrEmpty(customerFromKey) ? locCode : $"{customerFromKey}／{locCode}")
                    : $"{locCode} {locName}".Trim();
            }
            else if (string.IsNullOrEmpty(locName) && !string.IsNullOrEmpty(locCode))
            {
                // マスタに納入場所名称が無いときはコードだけだと意味が分かりにくいので注記する
                storeHeaders[kv.Key] = $"{locCode}（納入場所コード）";
            }
            else
                storeHeaders[kv.Key] = string.IsNullOrEmpty(baseH) ? kv.Key : baseH;
        }

        var storeKeys = storeHeaders.Keys
            .OrderBy(k => storeHeaders[k], StringComparer.Ordinal)
            .ThenBy(k => k, StringComparer.Ordinal)
            .ToList();

        var itemNameByCode = new Dictionary<string, string>(StringComparer.Ordinal);
        var aggregates = new Dictionary<(string ItemCode, string FoodType), Dictionary<string, decimal>>();

        foreach (var line in lines)
        {
            var cust = line.CustomerCode;
            var locCode = line.LocationCode;
            var locName = line.LocationName;
            if (string.IsNullOrEmpty(locCode) && string.IsNullOrEmpty(locName))
                continue;

            var sKey = StoreKey(cust, locCode);
            if (!storeHeaders.ContainsKey(sKey))
                continue;

            var itemCode = line.ItemCode;
            var itemName = line.ItemName;
            if (itemCode.Length > 0 && !itemNameByCode.ContainsKey(itemCode))
                itemNameByCode[itemCode] = itemName;

            var food = ResolveFoodTypeFromStrings(line.Addinfo02, line.Addinfo02Name);
            var groupKey = (itemCode, food);

            if (!aggregates.TryGetValue(groupKey, out var byStore))
            {
                byStore = new Dictionary<string, decimal>(StringComparer.Ordinal);
                aggregates[groupKey] = byStore;
            }

            byStore[sKey] = byStore.GetValueOrDefault(sKey) + line.Quantity;
        }

        var rows = aggregates
            .OrderBy(a => a.Key.ItemCode, StringComparer.Ordinal)
            .ThenBy(a => a.Key.FoodType, StringComparer.Ordinal)
            .Select(a => new SortingInquirySearchRowDto
            {
                ItemCode = a.Key.ItemCode,
                ItemName = itemNameByCode.GetValueOrDefault(a.Key.ItemCode) ?? "",
                FoodType = a.Key.FoodType,
                QuantitiesByStore = a.Value.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal)
            })
            .ToList();

        return new SortingInquirySearchResponseDto
        {
            StoreKeys = storeKeys,
            StoreHeaders = storeHeaders,
            Rows = rows
        };
    }

    private static string StoreKey(string customerCode, string locationCode) =>
        $"{customerCode}|{locationCode}";

    private static string CustomerCodeFromStoreKey(string storeKey)
    {
        var i = storeKey.IndexOf('|', StringComparison.Ordinal);
        return i <= 0 ? "" : storeKey[..i].Trim();
    }

    private static string ResolveFoodTypeFromAddinfo(SalesOrderLineAddinfo? addinfo) =>
        ResolveFoodTypeFromStrings(addinfo?.Addinfo02, addinfo?.Addinfo02Name);

    private static string ResolveFoodTypeFromStrings(string? addinfo02, string? addinfo02Name)
    {
        var name = (addinfo02Name ?? "").Trim();
        if (name.Length > 0)
            return name;
        return (addinfo02 ?? "").Trim();
    }

    private static DateOnly? ParseYyyymmdd(string? s)
    {
        if (string.IsNullOrEmpty(s) || s.Length != 8)
            return null;
        return DateOnly.TryParseExact(s, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d
            : null;
    }
}

/// <summary>仕分け照会の生 SQL 行（ID 列を読まない）。</summary>
internal sealed class SortingInquiryLineSqlRow
{
    public decimal Quantity { get; set; }
    public string? CustomerCode { get; set; }
    public string? LocationCode { get; set; }
    public string? LocationName { get; set; }
    public string? ItemCode { get; set; }
    public string? ItemName { get; set; }
    public string? Addinfo02 { get; set; }
    public string? Addinfo02Name { get; set; }
}

internal readonly record struct SortingInquiryLineMaterial(
    decimal Quantity,
    string CustomerCode,
    string LocationCode,
    string LocationName,
    string ItemCode,
    string ItemName,
    string? Addinfo02,
    string? Addinfo02Name);
