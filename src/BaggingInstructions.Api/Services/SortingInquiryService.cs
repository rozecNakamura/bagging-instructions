using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using BaggingInstructions.Api.Core;
using BaggingInstructions.Api.DTOs;
using BaggingInstructions.Api.Entities;

namespace BaggingInstructions.Api.Services;

/// <summary>
/// 仕分け照会: salesorderline を喫食日（planneddeliverydate）・便（slotcode）で検索し、
/// 品目×食種×（得意先コード＋納入場所コード）別に数量を集計する（同一納入場所キーに複数明細は合算）。
/// 一覧・Excel は品目コード・品目名称・食種のあと、検索結果に現れる各納入場所列を1列とする（列キーは得意先と納入場所コードの合成）。
/// 列見出し4段目は納入場所表示（名称優先、無ければコード。重複時は得意先コードで区別）。
/// 同一品目×食種行で複数納入場所列が並び、同日に複数拠点が同一品目を取引したことが分かる。
/// 最後に合計列とする。行データの食種キーは addinfo（<see cref="SalesOrderLineAddinfo.Addinfo02Name"/>／<see cref="SalesOrderLineAddinfo.Addinfo02"/>）。Excel の列見出しのみ「適用」とする。
/// 仕訳表 Excel の 1〜2 行目は <see cref="SortingInquirySearchResponseDto.StoreHeaderCodes"/>／<see cref="SortingInquirySearchResponseDto.StoreHeaders"/>（納入場所コード・表示名）。
/// 仕訳表用の列別収容は、明細ごとの単位0換算数量（<see cref="CookingInstructionQuantity.ResolveParentQtyInUnit0"/>）を
/// <see cref="SalesOrderLineAddinfo.Addinfo01"/> は DB 上は文字列だが、仕訳表の収容計算では正の数値としてパースして除算し、納入場所列ごとに合算する。
/// </summary>
public sealed class SortingInquiryService
{
    /// <summary>列キー用: 得意先コードと納入場所コードの区切り（JSON/API に含まれるが通常データに含まれない）。</summary>
    private const char DeliveryColumnKeySeparator = '\u001e';

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
            .SqlQuery<SortingInquiryCustomerHeaderSqlRow>($@"
SELECT
  x.custcode AS ""CustomerCode"",
  MAX(x.shortname) AS ""CustomerShortName"",
  MAX(x.fullname) AS ""CustomerNameFromMaster""
FROM (
  SELECT
    TRIM(COALESCE(s0.customercode, '')) AS custcode,
    NULLIF(TRIM(COALESCE(cu.customershortname, '')), '') AS shortname,
    NULLIF(TRIM(COALESCE(cu.customername, '')), '') AS fullname
  FROM salesorderline s
  INNER JOIN salesorder s0 ON s.salesorderid = s0.salesorderid
  LEFT JOIN customer cu ON s0.customercode = cu.customercode
  WHERE s.planneddeliverydate = {plannedDate}
    AND ({slotArr.Length} = 0
         OR NULLIF(TRIM(COALESCE(s.slotcode, '')), '') IS NULL
         OR TRIM(COALESCE(s.slotcode, '')) = ANY ({slotArr}))
) x
WHERE x.custcode <> ''
GROUP BY x.custcode
")
            .ToListAsync(ct);

        return rows
            .Select(r => (Code: (r.CustomerCode ?? "").Trim(), r))
            .Where(x => x.Code.Length > 0)
            .ToDictionary(
                x => x.Code,
                x => ResolveCustomerDisplayName(x.r.CustomerShortName, x.r.CustomerNameFromMaster, x.Code),
                StringComparer.Ordinal);
    }

    private async Task<Dictionary<string, string>> LoadCustomerHeadersInMemoryAsync(
        DateOnly plannedDate,
        HashSet<string> slots,
        CancellationToken ct)
    {
        var query = _db.SalesOrderLines
            .AsNoTracking()
            .Include(l => l.SalesOrder!)
                .ThenInclude(so => so!.Customer)
            .Where(l => l.PlannedDeliveryDate == plannedDate);

        if (slots.Count > 0)
            query = query.Where(l =>
                string.IsNullOrWhiteSpace(l.SlotCode) || slots.Contains(l.SlotCode!.Trim()));

        var lines = await query.ToListAsync(ct);
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var g in lines
                     .Select(l => (
                         Code: (l.SalesOrder?.CustomerCode ?? "").Trim(),
                         Cust: l.SalesOrder?.Customer))
                     .Where(t => t.Code.Length > 0)
                     .GroupBy(t => t.Code, StringComparer.Ordinal))
        {
            var first = g.First();
            dict[g.Key] = ResolveCustomerDisplayName(
                first.Cust?.CustomerShortName,
                first.Cust?.CustomerName,
                g.Key);
        }

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
  s.quantityuni0 AS ""QuantityUni0"",
  s.quantityuni1 AS ""QuantityUni1"",
  s.quantityuni2 AS ""QuantityUni2"",
  s.quantityuni3 AS ""QuantityUni3"",
  s0.customercode AS ""CustomerCode"",
  COALESCE(
    NULLIF(TRIM(COALESCE(c.locationcode, '')), ''),
    NULLIF(TRIM(COALESCE(s0.customerdeliverylocationcode, '')), '')
  ) AS ""LocationCode"",
  NULLIF(TRIM(COALESCE(c.locationname, '')), '') AS ""LocationName"",
  COALESCE(i.itemcode, '') AS ""ItemCode"",
  COALESCE(i.itemname, '') AS ""ItemName"",
  s1.addinfo01 AS ""Addinfo01"",
  s1.addinfo02 AS ""Addinfo02"",
  s1.addinfo02name AS ""Addinfo02Name"",
  i.conversionvalue1 AS ""ConversionValue1"",
  i.conversionvalue2 AS ""ConversionValue2"",
  i.conversionvalue3 AS ""ConversionValue3"",
  ia.car1 AS ""ItemIaCar1"",
  ia.car2 AS ""ItemIaCar2"",
  ia.car3 AS ""ItemIaCar3"",
  ia.car0 AS ""ItemCar0""
FROM salesorderline s
LEFT JOIN item i ON s.itemcode = i.itemcode
LEFT JOIN itemadditionalinformation ia ON ia.itemcode = i.itemcode
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

        return rows.Select(r => ToLineMaterial(
            r.Quantity,
            r.QuantityUni0,
            r.QuantityUni1,
            r.QuantityUni2,
            r.QuantityUni3,
            r.ItemIaCar1,
            r.ItemIaCar2,
            r.ItemIaCar3,
            r.ItemCar0,
            r.ConversionValue1,
            r.ConversionValue2,
            r.ConversionValue3,
            (r.CustomerCode ?? "").Trim(),
            (r.LocationCode ?? "").Trim(),
            (r.LocationName ?? "").Trim(),
            (r.ItemCode ?? "").Trim(),
            (r.ItemName ?? "").Trim(),
            r.Addinfo01,
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
                .ThenInclude(i => i!.AdditionalInformation)
            .Include(l => l.Addinfo)
            .Where(l => l.PlannedDeliveryDate == plannedDate);

        if (slots.Count > 0)
            query = query.Where(l =>
                string.IsNullOrWhiteSpace(l.SlotCode) || slots.Contains(l.SlotCode!.Trim()));

        var lines = await query
            .OrderBy(l => l.Item!.ItemCd ?? "")
            .ThenBy(l => l.SalesOrderLineId)
            .ToListAsync(ct);

        return lines.Select(l => ToLineMaterial(
            l.Quantity,
            l.QtyUni0,
            l.QtyUni1,
            l.QtyUni2,
            l.QtyUni3,
            l.Item?.AdditionalInformation?.Car1,
            l.Item?.AdditionalInformation?.Car2,
            l.Item?.AdditionalInformation?.Car3,
            l.Item?.AdditionalInformation?.Car0,
            l.Item?.ConversionValue1,
            l.Item?.ConversionValue2,
            l.Item?.ConversionValue3,
            (l.SalesOrder?.CustomerCode ?? "").Trim(),
            ResolveLocationCodeForSortingInquiry(l.SalesOrder),
            (l.SalesOrder?.CustomerDeliveryLocation?.LocationName ?? "").Trim(),
            (l.Item?.ItemCd ?? "").Trim(),
            (l.Item?.ItemName ?? "").Trim(),
            l.Addinfo?.Addinfo01,
            l.Addinfo?.Addinfo02,
            l.Addinfo?.Addinfo02Name)).ToList();
    }

    private static string SortingInquiryDeliveryColumnKey(string customerCode, string locationCode)
    {
        var c = (customerCode ?? "").Trim();
        var l = (locationCode ?? "").Trim();
        return c + DeliveryColumnKeySeparator + l;
    }

    private static SortingInquirySearchResponseDto BuildSearchResponse(
        IReadOnlyList<SortingInquiryLineMaterial> lines,
        IReadOnlyDictionary<string, string> customersOnDate)
    {
        if (customersOnDate.Count == 0 && lines.Count == 0)
            return new SortingInquirySearchResponseDto();

        var headerLabelByCustomer = new Dictionary<string, string>(customersOnDate, StringComparer.Ordinal);

        foreach (var line in lines)
        {
            var cust = (line.CustomerCode ?? "").Trim();
            if (cust.Length == 0)
                continue;
            if (!headerLabelByCustomer.ContainsKey(cust))
                headerLabelByCustomer[cust] = cust;
        }

        var columnCustomer = new Dictionary<string, string>(StringComparer.Ordinal);
        var columnLocationCode = new Dictionary<string, string>(StringComparer.Ordinal);
        var columnLocationTag = new Dictionary<string, string>(StringComparer.Ordinal);
        var columnCustomerDisplay = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var line in lines)
        {
            var cust = (line.CustomerCode ?? "").Trim();
            if (cust.Length == 0)
                continue;

            var loc = (line.LocationCode ?? "").Trim();
            var colKey = SortingInquiryDeliveryColumnKey(cust, loc);
            if (columnCustomer.ContainsKey(colKey))
                continue;

            columnCustomer[colKey] = cust;
            columnLocationCode[colKey] = loc;
            columnLocationTag[colKey] = DeliveryLocationHeaderLabel(line.LocationName, line.LocationCode);
            var disp = headerLabelByCustomer.TryGetValue(cust, out var dn) ? dn : cust;
            var t = (disp ?? "").Trim();
            columnCustomerDisplay[colKey] = t.Length > 0 ? t : cust;
        }

        if (columnCustomer.Count == 0)
            return new SortingInquirySearchResponseDto();

        var rawLocCodes = columnLocationCode.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
        var storeHeaderCodes = DisambiguateStoreRowLabels(rawLocCodes, StringComparer.Ordinal, columnCustomer);

        var storeHeaderDeliveryCodes = columnCustomer.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);

        // 仕分け照会 Excel 3 行目（得意先名）: 納入場所コードを括弧付きで付けない（1 行目で拠点を示す）。
        var storeHeaderDeliveryNames = columnCustomerDisplay.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);

        var rawLocTags = columnLocationTag.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
        var storeHeaders = DisambiguateStoreRowLabels(rawLocTags, StringComparer.Ordinal, columnCustomer);

        var storeKeys = columnCustomer.Keys
            .OrderBy(k => storeHeaders[k], StringComparer.Ordinal)
            .ThenBy(k => columnCustomer[k], StringComparer.Ordinal)
            .ThenBy(k => k, StringComparer.Ordinal)
            .ToList();

        var itemNameByCode = new Dictionary<string, string>(StringComparer.Ordinal);
        var aggregates = new Dictionary<(string ItemCode, string FoodType), Dictionary<string, decimal>>();
        var ratioAggregates = new Dictionary<(string ItemCode, string FoodType), Dictionary<string, decimal>>();

        foreach (var line in lines)
        {
            var cust = (line.CustomerCode ?? "").Trim();
            if (cust.Length == 0)
                continue;

            var colKey = SortingInquiryDeliveryColumnKey(cust, (line.LocationCode ?? "").Trim());
            if (!columnCustomer.ContainsKey(colKey))
                continue;

            var itemCode = line.ItemCode;
            var itemName = line.ItemName;
            if (itemCode.Length > 0 && !itemNameByCode.ContainsKey(itemCode))
                itemNameByCode[itemCode] = itemName;

            var food = ResolveFoodTypeFromStrings(line.Addinfo02, line.Addinfo02Name);
            var groupKey = (itemCode, food);

            if (!aggregates.TryGetValue(groupKey, out var byColumn))
            {
                byColumn = new Dictionary<string, decimal>(StringComparer.Ordinal);
                aggregates[groupKey] = byColumn;
            }

            byColumn[colKey] = byColumn.GetValueOrDefault(colKey) + line.Quantity;

            var div = ParseSortingInquiryAddinfoDivisor(line.Addinfo01);
            if (div.HasValue)
            {
                if (!ratioAggregates.TryGetValue(groupKey, out var byRatio))
                {
                    byRatio = new Dictionary<string, decimal>(StringComparer.Ordinal);
                    ratioAggregates[groupKey] = byRatio;
                }

                byRatio[colKey] = byRatio.GetValueOrDefault(colKey) + line.QtyInUnit0 / div.Value;
            }
        }

        var capacityByColumn = columnCustomer.Keys.ToDictionary(k => k, _ => 0m, StringComparer.Ordinal);
        foreach (var line in lines)
        {
            var cust = (line.CustomerCode ?? "").Trim();
            if (cust.Length == 0)
                continue;

            var colKey = SortingInquiryDeliveryColumnKey(cust, (line.LocationCode ?? "").Trim());
            if (!capacityByColumn.ContainsKey(colKey))
                continue;

            var div = ParseSortingInquiryAddinfoDivisor(line.Addinfo01);
            if (!div.HasValue)
                continue;
            capacityByColumn[colKey] += line.QtyInUnit0 / div.Value;
        }

        var rows = aggregates
            .OrderBy(a => a.Key.ItemCode, StringComparer.Ordinal)
            .ThenBy(a => a.Key.FoodType, StringComparer.Ordinal)
            .Select(a => new SortingInquirySearchRowDto
            {
                ItemCode = a.Key.ItemCode,
                ItemName = itemNameByCode.GetValueOrDefault(a.Key.ItemCode) ?? "",
                FoodType = a.Key.FoodType,
                QuantitiesByStore = a.Value.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal),
                RatioQuantitiesByStore = ratioAggregates.TryGetValue(a.Key, out var ratios)
                    ? ratios.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal)
                    : new Dictionary<string, decimal>(StringComparer.Ordinal)
            })
            .ToList();

        return new SortingInquirySearchResponseDto
        {
            StoreKeys = storeKeys,
            StoreHeaders = storeHeaders,
            StoreHeaderCodes = storeHeaderCodes,
            StoreHeaderDeliveryCodes = storeHeaderDeliveryCodes,
            StoreHeaderDeliveryNames = storeHeaderDeliveryNames,
            StoreHeaderCapacities = capacityByColumn,
            Rows = rows
        };
    }

    /// <summary>納入場所列見出し用: 名称優先、無ければコード。</summary>
    private static string DeliveryLocationHeaderLabel(string? locationName, string? locationCode)
    {
        var n = (locationName ?? "").Trim();
        if (n.Length > 0)
            return n;
        return (locationCode ?? "").Trim();
    }

    /// <summary>同一表示文字が複数列に付くときは「（suffix）」で区別。空はそのまま。</summary>
    /// <param name="disambiguationSuffixByKey">列キーごとの括弧内に付ける短い区別子（未指定時は列キー全体）。</param>
    private static Dictionary<string, string> DisambiguateStoreRowLabels(
        IReadOnlyDictionary<string, string> rawByKey,
        StringComparer cmp,
        IReadOnlyDictionary<string, string>? disambiguationSuffixByKey = null)
    {
        var nonEmpty = rawByKey
            .Where(kv => (kv.Value ?? "").Trim().Length > 0)
            .Select(kv => ((kv.Key), v: (kv.Value ?? "").Trim()))
            .ToList();
        var counts = nonEmpty
            .GroupBy(t => t.v, cmp)
            .ToDictionary(g => g.Key, g => g.Count(), cmp);

        var result = new Dictionary<string, string>(cmp);
        foreach (var key in rawByKey.Keys)
        {
            var disp = (rawByKey.TryGetValue(key, out var rv) ? rv : "").Trim();
            if (disp.Length == 0)
                result[key] = "";
            else if (counts.GetValueOrDefault(disp) > 1)
            {
                var suff = disambiguationSuffixByKey != null
                           && disambiguationSuffixByKey.TryGetValue(key, out var s)
                           && !string.IsNullOrEmpty((s ?? "").Trim())
                    ? (s ?? "").Trim()
                    : key;
                result[key] = $"{disp}（{suff}）";
            }
            else
                result[key] = disp;
        }

        return result;
    }

    private static string ResolveCustomerDisplayName(string? shortName, string? fullName, string? customerCode)
    {
        var f = (fullName ?? "").Trim();
        if (f.Length > 0)
            return f;
        var s = (shortName ?? "").Trim();
        if (s.Length > 0)
            return s;
        return (customerCode ?? "").Trim();
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

    /// <summary>
    /// 仕訳表収容用: <c>salesorderlineaddinfo.addinfo01</c> は DB では text/varchar だが、ここでは数値として解釈する（Excel 収容行の除数）。
    /// </summary>
    private static decimal? ParseSortingInquiryAddinfoDivisor(string? addinfo01)
    {
        if (string.IsNullOrWhiteSpace(addinfo01))
            return null;
        var t = addinfo01.Trim().Replace("\u00a0", "", StringComparison.Ordinal);
        const NumberStyles styles = NumberStyles.Number;
        if (decimal.TryParse(t, styles, CultureInfo.InvariantCulture, out var inv) && inv > 0)
            return inv;
        if (decimal.TryParse(t, styles, CultureInfo.GetCultureInfo("ja-JP"), out var ja) && ja > 0)
            return ja;
        return null;
    }

    private static SortingInquiryLineMaterial ToLineMaterial(
        decimal quantity,
        decimal? quantityUni0,
        decimal? quantityUni1,
        decimal? quantityUni2,
        decimal? quantityUni3,
        decimal? itemIaCar1,
        decimal? itemIaCar2,
        decimal? itemIaCar3,
        decimal? itemCar0,
        decimal? conversionValue1,
        decimal? conversionValue2,
        decimal? conversionValue3,
        string customerCode,
        string locationCode,
        string locationName,
        string itemCode,
        string itemName,
        string? addinfo01,
        string? addinfo02,
        string? addinfo02Name)
    {
        var qty0 = CookingInstructionQuantity.ResolveParentQtyInUnit0(
            quantity,
            quantityUni0,
            quantityUni1,
            quantityUni2,
            quantityUni3,
            itemIaCar1,
            itemIaCar2,
            itemIaCar3,
            itemCar0,
            conversionValue1,
            conversionValue2,
            conversionValue3);

        return new SortingInquiryLineMaterial(
            quantity,
            customerCode,
            locationCode,
            locationName,
            itemCode,
            itemName,
            addinfo02,
            addinfo02Name,
            addinfo01,
            qty0);
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

/// <summary>仕分け照会・得意先一覧（<c>salesorder.customercode</c> → <c>customer</c> 名称）。</summary>
internal sealed class SortingInquiryCustomerHeaderSqlRow
{
    public string? CustomerCode { get; set; }
    public string? CustomerShortName { get; set; }
    public string? CustomerNameFromMaster { get; set; }
}

/// <summary>仕分け照会の生 SQL 行（ID 列を読まない）。</summary>
internal sealed class SortingInquiryLineSqlRow
{
    public decimal Quantity { get; set; }
    public decimal? QuantityUni0 { get; set; }
    public decimal? QuantityUni1 { get; set; }
    public decimal? QuantityUni2 { get; set; }
    public decimal? QuantityUni3 { get; set; }
    public string? CustomerCode { get; set; }
    public string? LocationCode { get; set; }
    public string? LocationName { get; set; }
    public string? ItemCode { get; set; }
    public string? ItemName { get; set; }
    public string? Addinfo01 { get; set; }
    public string? Addinfo02 { get; set; }
    public string? Addinfo02Name { get; set; }
    public decimal? ConversionValue1 { get; set; }
    public decimal? ConversionValue2 { get; set; }
    public decimal? ConversionValue3 { get; set; }
    public decimal? ItemIaCar1 { get; set; }
    public decimal? ItemIaCar2 { get; set; }
    public decimal? ItemIaCar3 { get; set; }
    public decimal? ItemCar0 { get; set; }
}

internal readonly record struct SortingInquiryLineMaterial(
    decimal Quantity,
    string CustomerCode,
    string LocationCode,
    string LocationName,
    string ItemCode,
    string ItemName,
    string? Addinfo02,
    string? Addinfo02Name,
    string? Addinfo01,
    decimal QtyInUnit0);
