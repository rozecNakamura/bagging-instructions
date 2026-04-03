using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using BaggingInstructions.Api.Core;
using BaggingInstructions.Api.DTOs;
using BaggingInstructions.Api.Entities;

namespace BaggingInstructions.Api.Services;

/// <summary>
/// 仕分け照会: salesorderline を喫食日（planneddeliverydate）・便（slotcode）で検索し、
/// 品目×食種×得意先コード別に数量を集計する（同一得意先の複数納入場所は合算）。一覧・Excel は品目コード・品目名称・食種のあと、
/// 検索結果に現れる各得意先コードを1列とし、列見出しは納入場所名称（複数は「／」、名称が無い場合は場所コード、いずれも無い場合は得意先コード）。
/// 最後に合計列とする。食種は <see cref="SalesOrderLineAddinfo.Addinfo02Name"/>（無ければ <see cref="SalesOrderLineAddinfo.Addinfo02"/>）。
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

        IReadOnlyDictionary<string, string> customersOnDate;
        IReadOnlyList<SortingInquiryLineMaterial> materials;
        if (_db.Database.IsRelational())
        {
            customersOnDate = await LoadCustomerHeadersRelationalAsync(date.Value, slots, ct);
            materials = await LoadLinesForSearchRelationalAsync(date.Value, slots, ct);
        }
        else
        {
            customersOnDate = await LoadCustomerHeadersInMemoryAsync(date.Value, slots, ct);
            materials = await LoadLinesForSearchInMemoryAsync(date.Value, slots, ct);
        }

        return BuildSearchResponse(materials, customersOnDate);
    }

    /// <summary>
    /// 明細に addinfo 等で行が増えても漏れないよう、納入場所の有無に依存しない「その日・便条件の得意先一覧」を別経路で取得する。
    /// </summary>
    private async Task<Dictionary<string, string>> LoadCustomerHeadersRelationalAsync(
        DateOnly plannedDate,
        HashSet<string> slots,
        CancellationToken ct)
    {
        var slotArr = slots.Count > 0 ? slots.ToArray() : Array.Empty<string>();
        var rows = await _db.Database
            .SqlQuery<SortingInquiryCustomerCodeSqlRow>($@"
SELECT DISTINCT TRIM(COALESCE(s0.customercode, '')) AS ""CustomerCode""
FROM salesorderline s
INNER JOIN salesorder s0 ON s.salesorderid = s0.salesorderid
WHERE s.planneddeliverydate = {plannedDate}
  AND ({slotArr.Length} = 0
       OR NULLIF(TRIM(COALESCE(s.slotcode, '')), '') IS NULL
       OR TRIM(COALESCE(s.slotcode, '')) = ANY ({slotArr}))
")
            .ToListAsync(ct);

        return rows
            .Select(r => (r.CustomerCode ?? "").Trim())
            .Where(c => c.Length > 0)
            .ToDictionary(c => c, c => c, StringComparer.Ordinal);
    }

    private async Task<Dictionary<string, string>> LoadCustomerHeadersInMemoryAsync(
        DateOnly plannedDate,
        HashSet<string> slots,
        CancellationToken ct)
    {
        var query = _db.SalesOrderLines
            .AsNoTracking()
            .Include(l => l.SalesOrder!)
            .Where(l => l.PlannedDeliveryDate == plannedDate);

        if (slots.Count > 0)
            query = query.Where(l =>
                string.IsNullOrWhiteSpace(l.SlotCode) || slots.Contains(l.SlotCode!.Trim()));

        var lines = await query.ToListAsync(ct);
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var code in lines
                     .Select(l => (l.SalesOrder?.CustomerCode ?? "").Trim())
                     .Where(c => c.Length > 0)
                     .Distinct(StringComparer.Ordinal))
            dict[code] = code;

        return dict;
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
  COALESCE(
    NULLIF(TRIM(COALESCE(c.locationcode, '')), ''),
    NULLIF(TRIM(COALESCE(s0.customerdeliverylocationcode, '')), '')
  ) AS ""LocationCode"",
  NULLIF(TRIM(COALESCE(c.locationname, '')), '') AS ""LocationName"",
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
  AND ({slotArr.Length} = 0
       OR NULLIF(TRIM(COALESCE(s.slotcode, '')), '') IS NULL
       OR TRIM(COALESCE(s.slotcode, '')) = ANY ({slotArr}))
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
            query = query.Where(l =>
                string.IsNullOrWhiteSpace(l.SlotCode) || slots.Contains(l.SlotCode!.Trim()));

        var lines = await query
            .OrderBy(l => l.Item!.ItemCd ?? "")
            .ThenBy(l => l.SalesOrderLineId)
            .ToListAsync(ct);

        return lines.Select(l => new SortingInquiryLineMaterial(
            l.Quantity,
            (l.SalesOrder?.CustomerCode ?? "").Trim(),
            ResolveLocationCodeForSortingInquiry(l.SalesOrder),
            (l.SalesOrder?.CustomerDeliveryLocation?.LocationName ?? "").Trim(),
            (l.Item?.ItemCd ?? "").Trim(),
            (l.Item?.ItemName ?? "").Trim(),
            l.Addinfo?.Addinfo02,
            l.Addinfo?.Addinfo02Name)).ToList();
    }

    private static SortingInquirySearchResponseDto BuildSearchResponse(
        IReadOnlyList<SortingInquiryLineMaterial> lines,
        IReadOnlyDictionary<string, string> customersOnDate)
    {
        if (customersOnDate.Count == 0 && lines.Count == 0)
            return new SortingInquirySearchResponseDto();

        var headerLabelByCustomer = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var kv in customersOnDate)
            headerLabelByCustomer[kv.Key] = kv.Key;

        foreach (var line in lines)
        {
            var cust = (line.CustomerCode ?? "").Trim();
            if (cust.Length == 0)
                continue;
            if (!headerLabelByCustomer.ContainsKey(cust))
                headerLabelByCustomer[cust] = cust;
        }

        var locationTagsByCustomer = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var locationCodesByCustomer = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var line in lines)
        {
            var cust = (line.CustomerCode ?? "").Trim();
            if (cust.Length == 0)
                continue;
            var tag = DeliveryLocationHeaderLabel(line.LocationName, line.LocationCode);
            if (tag.Length > 0)
            {
                if (!locationTagsByCustomer.TryGetValue(cust, out var set))
                {
                    set = new HashSet<string>(StringComparer.Ordinal);
                    locationTagsByCustomer[cust] = set;
                }

                set.Add(tag);
            }

            var locCode = (line.LocationCode ?? "").Trim();
            if (locCode.Length > 0)
            {
                if (!locationCodesByCustomer.TryGetValue(cust, out var codeSet))
                {
                    codeSet = new HashSet<string>(StringComparer.Ordinal);
                    locationCodesByCustomer[cust] = codeSet;
                }

                codeSet.Add(locCode);
            }
        }

        foreach (var code in headerLabelByCustomer.Keys.ToArray())
        {
            if (locationTagsByCustomer.TryGetValue(code, out var set) && set.Count > 0)
                headerLabelByCustomer[code] = string.Join("／", set.OrderBy(s => s, StringComparer.Ordinal));
        }

        var nameLabelCounts = headerLabelByCustomer.Values
            .Select(v => (v ?? "").Trim())
            .GroupBy(v => v, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        var storeHeaders = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var kv in headerLabelByCustomer)
        {
            var code = kv.Key;
            var disp = (kv.Value ?? "").Trim();
            if (string.IsNullOrEmpty(disp))
                storeHeaders[code] = code;
            else if (nameLabelCounts.GetValueOrDefault(disp) > 1)
                storeHeaders[code] = $"{disp}（{code}）";
            else
                storeHeaders[code] = disp;
        }

        var storeKeys = storeHeaders.Keys
            .OrderBy(k => storeHeaders[k], StringComparer.Ordinal)
            .ThenBy(k => k, StringComparer.Ordinal)
            .ToList();

        var headerCodeJoin = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var cust in storeHeaders.Keys)
        {
            if (locationCodesByCustomer.TryGetValue(cust, out var cset) && cset.Count > 0)
                headerCodeJoin[cust] = string.Join("／", cset.OrderBy(s => s, StringComparer.Ordinal));
            else
                headerCodeJoin[cust] = cust;
        }

        var codeLabelCounts = headerCodeJoin.Values
            .Select(v => (v ?? "").Trim())
            .GroupBy(v => v, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        var storeHeaderCodes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var kv in headerCodeJoin)
        {
            var code = kv.Key;
            var disp = (kv.Value ?? "").Trim();
            if (string.IsNullOrEmpty(disp))
                storeHeaderCodes[code] = code;
            else if (codeLabelCounts.GetValueOrDefault(disp) > 1)
                storeHeaderCodes[code] = $"{disp}（{code}）";
            else
                storeHeaderCodes[code] = disp;
        }

        var itemNameByCode = new Dictionary<string, string>(StringComparer.Ordinal);
        var aggregates = new Dictionary<(string ItemCode, string FoodType), Dictionary<string, decimal>>();

        foreach (var line in lines)
        {
            var cust = (line.CustomerCode ?? "").Trim();
            if (string.IsNullOrEmpty(cust) || !storeHeaders.ContainsKey(cust))
                continue;

            var itemCode = line.ItemCode;
            var itemName = line.ItemName;
            if (itemCode.Length > 0 && !itemNameByCode.ContainsKey(itemCode))
                itemNameByCode[itemCode] = itemName;

            var food = ResolveFoodTypeFromStrings(line.Addinfo02, line.Addinfo02Name);
            var groupKey = (itemCode, food);

            if (!aggregates.TryGetValue(groupKey, out var byCustomer))
            {
                byCustomer = new Dictionary<string, decimal>(StringComparer.Ordinal);
                aggregates[groupKey] = byCustomer;
            }

            byCustomer[cust] = byCustomer.GetValueOrDefault(cust) + line.Quantity;
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
            StoreHeaderCodes = storeHeaderCodes,
            Rows = rows
        };
    }

    /// <summary>列見出し用: 納入場所名称を優先し、無ければ場所コード。</summary>
    private static string DeliveryLocationHeaderLabel(string? locationName, string? locationCode)
    {
        var n = (locationName ?? "").Trim();
        if (n.Length > 0)
            return n;
        return (locationCode ?? "").Trim();
    }

    /// <summary>
    /// 納入場所マスタに無い／JOIN で取れない行でも、受注の customerdeliverylocationcode で列キーを分け、
    /// 得意先ごとに別列として集計できるようにする。
    /// </summary>
    private static string ResolveLocationCodeForSortingInquiry(SalesOrder? salesOrder)
    {
        var fromMaster = (salesOrder?.CustomerDeliveryLocation?.LocationCode ?? "").Trim();
        if (fromMaster.Length > 0)
            return fromMaster;
        return (salesOrder?.CustomerDeliveryLocationCode ?? "").Trim();
    }

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

/// <summary>仕分け照会・その日の得意先コード一覧（DISTINCT）。</summary>
internal sealed class SortingInquiryCustomerCodeSqlRow
{
    public string? CustomerCode { get; set; }
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
