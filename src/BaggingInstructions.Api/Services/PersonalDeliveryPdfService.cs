using System.Globalization;
using BaggingInstructions.Api.Core;
using BaggingInstructions.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace BaggingInstructions.Api.Services;

/// <summary>
/// 個人配送指示書.rxz / 個人配送指示書（集計）.rxz 用の PDF 生成。
/// 明細: cstmeat 基準・得意先300・addinfo08先頭1。
/// 集計: cstmeat 基準・得意先300/310・主菜/主食/汁物別集計。
/// </summary>
public class PersonalDeliveryPdfService
{
    private const int RowsPerPage = 22;
    private const int MaxRows = 40;

    private static readonly IReadOnlySet<string> PrintHeaderShrinkToFitFields =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "PAGECOUNT",
            "PRINTDATE",
            "PRINTTIME",
        };

    private readonly AppDbContext _appDb;
    private readonly CstmeatDbContext _cstmeatDb;
    private readonly JuicePdfService _juicePdfService;

    public PersonalDeliveryPdfService(AppDbContext appDb, CstmeatDbContext cstmeatDb, JuicePdfService juicePdfService)
    {
        _appDb = appDb;
        _cstmeatDb = cstmeatDb;
        _juicePdfService = juicePdfService;
    }

    /// <summary>選択された (喫食日, 喫食時間, コース) ごとにPDFを生成。</summary>
    public async Task<byte[]> GeneratePdfAsync(
        string templateDetailPath,
        string templateSummaryPath,
        IReadOnlyList<(string EatingDate, string MealTime, string Course)> selections,
        string variant,
        CancellationToken ct = default)
    {
        if (selections == null || selections.Count == 0)
            return Array.Empty<byte>();

        var isDetail = PersonalDeliveryHelper.IsDetailVariant(variant);
        var isSummary = string.Equals(variant, "summary", StringComparison.OrdinalIgnoreCase);
        var printNow = DateTime.Now;

        if (isDetail)
        {
            var foodTypeNameMap = await LoadFoodTypeNameMapAsync(ct);
            var pagePlans = new List<(List<PersonalDeliveryLine> Lines, int PageIndex)>();
            foreach (var (eatingDate, mealTime, course) in selections)
            {
                var lines = await LoadDetailLinesAsync(eatingDate, mealTime, course, foodTypeNameMap, ct);
                if (lines.Count == 0) continue;

                for (int pageIndex = 0; pageIndex * RowsPerPage < lines.Count; pageIndex++)
                    pagePlans.Add((lines, pageIndex));
            }

            if (pagePlans.Count == 0)
                return Array.Empty<byte>();

            var pagesTagValues = new List<Dictionary<string, string>>();
            var totalPages = pagePlans.Count;
            for (int i = 0; i < pagePlans.Count; i++)
            {
                var (lines, pageIndex) = pagePlans[i];
                var tags = BuildDetailTagValuesForPage(lines, pageIndex);
                AddPersonalDeliveryPrintTags(tags, printNow, i + 1, totalPages);
                pagesTagValues.Add(tags);
            }

            return _juicePdfService.GeneratePdfMultiPage(
                templateDetailPath,
                pagesTagValues,
                "個人配送指示書",
                shrinkToFitOverrides: PrintHeaderShrinkToFitFields,
                textLayoutFieldFilter: ShouldApplyTextLayout);
        }

        if (isSummary)
        {
            var foodTypeNameMap = await LoadFoodTypeNameMapAsync(ct);
            var pagesTagValues = new List<Dictionary<string, string>>();
            foreach (var (eatingDate, mealTime, course) in selections)
            {
                var summaryLines = await LoadSummaryLinesAsync(eatingDate, mealTime, course, foodTypeNameMap, ct);
                if (summaryLines.Count == 0) continue;
                pagesTagValues.Add(BuildSummaryTagValues(summaryLines));
            }

            if (pagesTagValues.Count == 0)
                return Array.Empty<byte>();

            var totalPages = pagesTagValues.Count;
            for (int i = 0; i < pagesTagValues.Count; i++)
                AddPersonalDeliveryPrintTags(pagesTagValues[i], printNow, i + 1, totalPages);

            return _juicePdfService.GeneratePdfMultiPage(
                templateSummaryPath,
                pagesTagValues,
                "個人配送指示書（集計）",
                shrinkToFitOverrides: PrintHeaderShrinkToFitFields,
                textLayoutFieldFilter: ShouldApplyTextLayout);
        }

        return Array.Empty<byte>();
    }

    /// <summary>
    /// 調理指示書と同様、テンプレートの AutoLineFeed / ShrinkToFit を尊重しつつ、
    /// データ項目には折り返し・縮小表示を適用する対象フィールドか。
    /// </summary>
    public static bool ShouldApplyTextLayout(string fieldName)
    {
        if (string.IsNullOrEmpty(fieldName)) return false;

        return fieldName.StartsWith("CUSTOMERNM", StringComparison.OrdinalIgnoreCase)
            || fieldName.StartsWith("CUSTOMERLOC", StringComparison.OrdinalIgnoreCase)
            || fieldName.StartsWith("FOODTYPE", StringComparison.OrdinalIgnoreCase)
            || fieldName.StartsWith("RICETYPE", StringComparison.OrdinalIgnoreCase)
            || fieldName.StartsWith("GRAM", StringComparison.OrdinalIgnoreCase)
            || fieldName.StartsWith("NOTE", StringComparison.OrdinalIgnoreCase)
            || fieldName.StartsWith("ORDER", StringComparison.OrdinalIgnoreCase)
            || fieldName.StartsWith("COUNT", StringComparison.OrdinalIgnoreCase)
            || fieldName.Equals("DATE", StringComparison.OrdinalIgnoreCase)
            || fieldName.Equals("TIME", StringComparison.OrdinalIgnoreCase)
            || fieldName.Equals("AREA", StringComparison.OrdinalIgnoreCase)
            || fieldName.Equals("PAGECOUNT", StringComparison.OrdinalIgnoreCase)
            || fieldName.Equals("PRINTDATE", StringComparison.OrdinalIgnoreCase)
            || fieldName.Equals("PRINTTIME", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>調理指示書と同様、ページ情報は短い形式（N/M）で印字する。</summary>
    private static void AddPersonalDeliveryPrintTags(
        Dictionary<string, string> tagValues,
        DateTime printNow,
        int currentPage,
        int totalPages)
    {
        JuicePdfService.AddPrintTags(tagValues, printNow, currentPage, totalPages);
        tagValues["PAGECOUNT"] = $"{currentPage}/{totalPages}";
    }

    private async Task<List<PersonalDeliveryLine>> LoadDetailLinesAsync(
        string eatingDate,
        string mealTime,
        string course,
        IReadOnlyDictionary<string, string> foodTypeNameMap,
        CancellationToken ct)
    {
        if (eatingDate.Length != 8)
            return new List<PersonalDeliveryLine>();

        if (!DateOnly.TryParseExact(eatingDate, "yyyyMMdd", null, DateTimeStyles.None, out var date))
            return new List<PersonalDeliveryLine>();

        var cstmeatRows = await LoadCstmeatRowsAsync(eatingDate, isSummary: false, ct);
        if (cstmeatRows.Count == 0)
            return new List<PersonalDeliveryLine>();

        var locationCodes = ExtractLocationCodes(cstmeatRows);
        var customerCodes = new[] { PersonalDeliveryHelper.TargetCustomerCode };
        var addinfoMap = await LoadLocationAddinfoMapAsync(customerCodes, locationCodes, ct);
        var locations = await LoadLocationsAsync(customerCodes, locationCodes, ct);
        var salesLines = await LoadSalesOrderLinesAsync(date, customerCodes, ct);
        var lineIndex = BuildSalesLineIndex(salesLines, requireAddinfo08: true);
        var minorClassNameMap = await LoadMinorClassificationNameMapAsync(salesLines, ct);

        return BuildDetailLines(cstmeatRows, addinfoMap, locations, lineIndex, minorClassNameMap, foodTypeNameMap, mealTime, course);
    }

    private async Task<List<PersonalDeliverySummaryLine>> LoadSummaryLinesAsync(
        string eatingDate,
        string mealTime,
        string course,
        IReadOnlyDictionary<string, string> foodTypeNameMap,
        CancellationToken ct)
    {
        if (eatingDate.Length != 8)
            return new List<PersonalDeliverySummaryLine>();

        if (!DateOnly.TryParseExact(eatingDate, "yyyyMMdd", null, DateTimeStyles.None, out var date))
            return new List<PersonalDeliverySummaryLine>();

        var cstmeatRows = await LoadCstmeatRowsAsync(eatingDate, isSummary: true, ct);
        if (cstmeatRows.Count == 0)
            return new List<PersonalDeliverySummaryLine>();

        var customerCodes = PersonalDeliveryHelper.SummaryTargetCustomerCodes;
        var locationCodes = ExtractLocationCodes(cstmeatRows);
        var addinfoMap = await LoadLocationAddinfoMapAsync(customerCodes, locationCodes, ct);
        var info19Map = BuildInfo19Map(cstmeatRows);
        var activeLocationKeys = cstmeatRows
            .Select(r => ((r.Info01 ?? "").Trim(), (r.Info02 ?? "").Trim()))
            .Where(k => k.Item1.Length > 0 && k.Item2.Length > 0)
            .ToHashSet();

        var salesLines = await LoadSalesOrderLinesAsync(date, customerCodes, ct);
        var mealTimeNorm = (mealTime ?? "").Trim();
        var courseNorm = (course ?? "").Trim();
        var aggregates = new Dictionary<(PersonalDeliveryHelper.SummaryItemCategory Category, string FoodTypeCode), PersonalDeliverySummaryLine>();
        var locationKeysByAggregate =
            new Dictionary<(PersonalDeliveryHelper.SummaryItemCategory Category, string FoodTypeCode), HashSet<(string CustomerCode, string LocationCode)>>();
        var notesByAggregate =
            new Dictionary<(PersonalDeliveryHelper.SummaryItemCategory Category, string FoodTypeCode), HashSet<string>>();

        foreach (var line in salesLines)
        {
            var custCode = (line.SalesOrder?.CustomerCode ?? "").Trim();
            var locCode = (line.SalesOrder?.CustomerDeliveryLocation?.LocationCode ?? "").Trim();
            if (custCode.Length == 0 || locCode.Length == 0) continue;
            if (!activeLocationKeys.Contains((custCode, locCode))) continue;
            if ((line.Addinfo?.Addinfo05 ?? "").Trim() != mealTimeNorm) continue;

            addinfoMap.TryGetValue((custCode, locCode), out var addinfo);
            info19Map.TryGetValue((custCode, locCode), out var info19);
            var (resolvedCourse, _) = PersonalDeliveryHelper.ResolveCourseAndOrder(info19, addinfo);
            if (resolvedCourse != courseNorm) continue;

            var foodTypeCode = (line.Addinfo?.Addinfo02 ?? "").Trim();
            var category = PersonalDeliveryHelper.ResolveSummaryItemCategory(line.Item?.ItemCd);
            var key = (category, foodTypeCode);

            if (!aggregates.TryGetValue(key, out var summaryLine))
            {
                summaryLine = new PersonalDeliverySummaryLine
                {
                    EatingDate = FormatEatingDate(eatingDate),
                    MealTime = mealTimeNorm,
                    MealTimeName = BaggingEatingTimeLabel.MapFromAddinfo05(mealTimeNorm),
                    Course = resolvedCourse,
                    Category = category,
                    FoodTypeCode = foodTypeCode,
                    FoodType = ResolveFoodTypeDisplayName(line, foodTypeCode, foodTypeNameMap)
                };
                aggregates[key] = summaryLine;
                locationKeysByAggregate[key] = new HashSet<(string, string)>();
                notesByAggregate[key] = new HashSet<string>(StringComparer.Ordinal);
            }

            locationKeysByAggregate[key].Add((custCode, locCode));

            if (category == PersonalDeliveryHelper.SummaryItemCategory.StapleFood)
            {
                var gram = (line.Addinfo?.Addinfo01 ?? "").Trim();
                if (gram.Length > 0 && string.IsNullOrEmpty(summaryLine.GramAmount))
                    summaryLine.GramAmount = gram;
            }
        }

        foreach (var cm in cstmeatRows)
        {
            var custCode = (cm.Info01 ?? "").Trim();
            var locCode = (cm.Info02 ?? "").Trim();
            if (custCode.Length == 0 || locCode.Length == 0) continue;
            if ((cm.Info04 ?? "").Trim() != mealTimeNorm) continue;

            addinfoMap.TryGetValue((custCode, locCode), out var addinfo);
            info19Map.TryGetValue((custCode, locCode), out var info19);
            var (resolvedCourse, _) = PersonalDeliveryHelper.ResolveCourseAndOrder(info19, addinfo);
            if (resolvedCourse != courseNorm) continue;

            var foodTypeCode = (cm.Info05 ?? "").Trim();
            if (foodTypeCode.Length == 0) continue;
            if (!TryParseCstmeatQuantity(cm.Info07, out var qty) || qty == 0) continue;

            var note = (cm.Info17 ?? "").Trim();
            var locationKey = (custCode, locCode);

            foreach (var (key, summaryLine) in aggregates)
            {
                if (!string.Equals(key.FoodTypeCode, foodTypeCode, StringComparison.Ordinal)) continue;
                if (!locationKeysByAggregate[key].Contains(locationKey)) continue;

                summaryLine.TotalQuantity += qty;
                if (note.Length > 0 && notesByAggregate[key].Add(note))
                    summaryLine.Note = string.Join(" ", notesByAggregate[key]);
            }
        }

        return aggregates.Values
            .Where(l => l.TotalQuantity != 0)
            .OrderBy(l => PersonalDeliveryHelper.GetSummaryCategorySortOrder(l.Category))
            .ThenBy(l => l.EatingDate, StringComparer.Ordinal)
            .ThenBy(l => l.MealTime, StringComparer.Ordinal)
            .ThenBy(l => l.Course, StringComparer.Ordinal)
            .ThenBy(l => l.FoodTypeCode, StringComparer.Ordinal)
            .ToList();
    }

    private static List<PersonalDeliveryLine> BuildDetailLines(
        IReadOnlyList<Cstmeat> cstmeatRows,
        IReadOnlyDictionary<(string CustomerCode, string LocationCode), CustomerDeliveryLocationAddinfo> addinfoMap,
        IReadOnlyDictionary<string, CustomerDeliveryLocation> locations,
        IReadOnlyDictionary<(string LocCode, string MealTime, string FoodType), List<SalesOrderLine>> lineIndex,
        IReadOnlyDictionary<string, string> minorClassNameMap,
        IReadOnlyDictionary<string, string> foodTypeNameMap,
        string mealTime,
        string course)
    {
        var mealTimeNorm = (mealTime ?? "").Trim();
        var courseNorm = (course ?? "").Trim();
        var result = new List<PersonalDeliveryLine>();

        foreach (var cm in cstmeatRows)
        {
            var custCode = (cm.Info01 ?? "").Trim();
            var locCode = (cm.Info02 ?? "").Trim();
            if (locCode.Length == 0) continue;
            if ((cm.Info04 ?? "").Trim() != mealTimeNorm) continue;
            if (!addinfoMap.TryGetValue((custCode, locCode), out var addinfo)) continue;
            if (!PersonalDeliveryHelper.IsTargetAddinfo08(addinfo.Addinfo08)) continue;

            var (resolvedCourse, deliveryOrder) = PersonalDeliveryHelper.ResolveCourseAndOrder(cm.Info19, addinfo);
            if (resolvedCourse != courseNorm) continue;

            locations.TryGetValue(locCode, out var location);
            var foodTypeCode = (cm.Info05 ?? "").Trim();
            lineIndex.TryGetValue((locCode, mealTimeNorm, foodTypeCode), out var matchedLines);
            var riceLine = matchedLines?.FirstOrDefault(l => PersonalDeliveryHelper.IsRiceItemCode(l.Item?.ItemCd));
            var displayLine = riceLine ?? matchedLines?.FirstOrDefault();

            result.Add(new PersonalDeliveryLine
            {
                EatingDate = FormatEatingDate(cm.Info03),
                MealTime = mealTimeNorm,
                MealTimeName = BaggingEatingTimeLabel.MapFromAddinfo05(mealTimeNorm),
                Course = resolvedCourse,
                DeliveryOrder = deliveryOrder,
                LocationCode = locCode,
                LocationName = location?.LocationName ?? "",
                Address1 = location?.Address1 ?? "",
                Address2 = location?.Address2 ?? "",
                CustomerName = location?.Customer?.CustomerName ?? "",
                CustomerAddress1 = location?.Customer?.Address1 ?? "",
                CustomerAddress2 = location?.Customer?.Address2 ?? "",
                FoodTypeCode = foodTypeCode,
                FoodType = ResolveFoodTypeDisplayName(displayLine, foodTypeCode, foodTypeNameMap),
                RiceType = ResolveRiceTypeName(riceLine, minorClassNameMap),
                RiceAmount = riceLine?.Addinfo?.Addinfo01 ?? "",
                HasRiceItem = riceLine != null,
                Remarks = (cm.Info17 ?? "").Trim()
            });
        }

        return result
            .OrderBy(l => l.EatingDate, StringComparer.Ordinal)
            .ThenBy(l => l.MealTime, StringComparer.Ordinal)
            .ThenBy(l => l.Course, StringComparer.Ordinal)
            .ThenBy(l => l.DeliveryOrder, Comparer<string>.Create(PersonalDeliveryHelper.CompareDeliveryOrder))
            .ThenBy(l => l.LocationCode, StringComparer.Ordinal)
            .ThenBy(l => l.FoodTypeCode, StringComparer.Ordinal)
            .ToList();
    }

    private async Task<List<Cstmeat>> LoadCstmeatRowsAsync(string eatingDate, bool isSummary, CancellationToken ct)
    {
        var query = _cstmeatDb.Cstmeats
            .AsNoTracking()
            .Where(c => c.Info03 == eatingDate);

        if (isSummary)
        {
            var codes = PersonalDeliveryHelper.SummaryTargetCustomerCodes;
            return await query
                .Where(c => c.Info01 != null && codes.Contains(c.Info01.Trim()))
                .ToListAsync(ct);
        }

        return await query
            .Where(c => c.Info01 != null && c.Info01.Trim() == PersonalDeliveryHelper.TargetCustomerCode)
            .ToListAsync(ct);
    }

    private static List<string> ExtractLocationCodes(IReadOnlyList<Cstmeat> cstmeatRows) =>
        cstmeatRows
            .Select(r => (r.Info02 ?? "").Trim())
            .Where(c => c.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    private static Dictionary<(string CustomerCode, string LocationCode), string> BuildInfo19Map(
        IReadOnlyList<Cstmeat> cstmeatRows)
    {
        var map = new Dictionary<(string, string), string>();
        foreach (var row in cstmeatRows)
        {
            var cust = (row.Info01 ?? "").Trim();
            var loc = (row.Info02 ?? "").Trim();
            if (cust.Length == 0 || loc.Length == 0) continue;
            var key = (cust, loc);
            if (!map.ContainsKey(key))
                map[key] = (row.Info19 ?? "").Trim();
        }
        return map;
    }

    private async Task<Dictionary<(string CustomerCode, string LocationCode), CustomerDeliveryLocationAddinfo>>
        LoadLocationAddinfoMapAsync(
            IReadOnlyList<string> customerCodes,
            IReadOnlyList<string> locationCodes,
            CancellationToken ct)
    {
        if (locationCodes.Count == 0 || customerCodes.Count == 0)
            return new Dictionary<(string, string), CustomerDeliveryLocationAddinfo>();

        var addinfos = await _appDb.CustomerDeliveryLocationAddinfos
            .AsNoTracking()
            .Where(a => customerCodes.Contains(a.CustomerCode))
            .Where(a => locationCodes.Contains(a.LocationCode))
            .ToListAsync(ct);

        return addinfos
            .GroupBy(a => ((a.CustomerCode ?? "").Trim(), (a.LocationCode ?? "").Trim()))
            .Where(g => g.Key.Item1.Length > 0 && g.Key.Item2.Length > 0)
            .ToDictionary(g => g.Key, g => g.First());
    }

    private async Task<Dictionary<string, CustomerDeliveryLocation>> LoadLocationsAsync(
        IReadOnlyList<string> customerCodes,
        IReadOnlyList<string> locationCodes,
        CancellationToken ct)
    {
        var locations = await _appDb.CustomerDeliveryLocations
            .AsNoTracking()
            .Include(l => l.Customer)
            .Where(l => customerCodes.Contains(l.CustomerCode ?? ""))
            .Where(l => locationCodes.Contains(l.LocationCode ?? ""))
            .ToListAsync(ct);

        return locations
            .GroupBy(l => (l.LocationCode ?? "").Trim(), StringComparer.Ordinal)
            .Where(g => g.Key.Length > 0)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
    }

    private async Task<List<SalesOrderLine>> LoadSalesOrderLinesAsync(
        DateOnly date,
        IReadOnlyList<string> customerCodes,
        CancellationToken ct)
    {
        return await _appDb.SalesOrderLines
            .AsNoTracking()
            .Include(l => l.Addinfo)
            .Include(l => l.SalesOrder!)
                .ThenInclude(so => so!.Customer)
            .Include(l => l.SalesOrder!)
                .ThenInclude(so => so!.CustomerDeliveryLocation!)
                .ThenInclude(loc => loc!.Addinfo)
            .Include(l => l.Item!)
                .ThenInclude(i => i!.AdditionalInformation)
            .Where(l => l.PlannedDeliveryDate == date)
            .Where(l => l.SalesOrder != null && customerCodes.Contains(l.SalesOrder.CustomerCode ?? ""))
            .ToListAsync(ct);
    }

    private static Dictionary<(string LocCode, string MealTime, string FoodType), List<SalesOrderLine>> BuildSalesLineIndex(
        IReadOnlyList<SalesOrderLine> lines,
        bool requireAddinfo08)
    {
        var dict = new Dictionary<(string, string, string), List<SalesOrderLine>>();
        foreach (var line in lines)
        {
            var locCode = (line.SalesOrder?.CustomerDeliveryLocation?.LocationCode ?? "").Trim();
            if (locCode.Length == 0) continue;
            if (requireAddinfo08 &&
                !PersonalDeliveryHelper.IsTargetAddinfo08(line.SalesOrder?.CustomerDeliveryLocation?.Addinfo?.Addinfo08))
                continue;

            var mealTime = (line.Addinfo?.Addinfo05 ?? "").Trim();
            var foodType = (line.Addinfo?.Addinfo02 ?? "").Trim();
            var key = (locCode, mealTime, foodType);
            if (!dict.TryGetValue(key, out var list))
            {
                list = new List<SalesOrderLine>();
                dict[key] = list;
            }
            list.Add(line);
        }
        return dict;
    }

    private async Task<IReadOnlyDictionary<string, string>> LoadMinorClassificationNameMapAsync(
        IReadOnlyList<SalesOrderLine> lines,
        CancellationToken ct)
    {
        var minorCodes = lines
            .Where(l => PersonalDeliveryHelper.IsRiceItemCode(l.Item?.ItemCd))
            .Select(l => (l.Item?.MinorClassificationCode ?? "").Trim())
            .Where(c => c.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (minorCodes.Count == 0)
            return new Dictionary<string, string>(StringComparer.Ordinal);

        var rows = await _appDb.MinorClassifications
            .AsNoTracking()
            .Where(m => m.MinorClassificationCode != null && minorCodes.Contains(m.MinorClassificationCode))
            .ToListAsync(ct);

        return rows
            .Where(m => !string.IsNullOrWhiteSpace(m.MinorClassificationCode))
            .GroupBy(m => m.MinorClassificationCode!.Trim(), StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => (g.First().MinorClassificationName ?? "").Trim(),
                StringComparer.Ordinal);
    }

    private static string ResolveRiceTypeName(
        SalesOrderLine? riceLine,
        IReadOnlyDictionary<string, string> minorClassNameMap)
    {
        if (riceLine == null || !PersonalDeliveryHelper.IsRiceItemCode(riceLine.Item?.ItemCd))
            return "";

        var minorCode = (riceLine.Item?.MinorClassificationCode ?? "").Trim();
        if (minorCode.Length == 0)
            return "";

        return minorClassNameMap.TryGetValue(minorCode, out var name) ? name : "";
    }

    private async Task<IReadOnlyDictionary<string, string>> LoadFoodTypeNameMapAsync(CancellationToken ct)
    {
        var rows = await _cstmeatDb.Foodtypes
            .AsNoTracking()
            .Where(f => f.Foodtypecd != null && f.Foodtypecd != "")
            .ToListAsync(ct);

        return rows
            .GroupBy(f => f.Foodtypecd!.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => (g.First().Foodtypename ?? "").Trim(),
                StringComparer.OrdinalIgnoreCase);
    }

    private static string ResolveFoodTypeDisplayName(
        SalesOrderLine? line,
        string foodTypeCode,
        IReadOnlyDictionary<string, string> foodTypeNameMap)
    {
        var name = (line?.Addinfo?.Addinfo02Name ?? "").Trim();
        if (name.Length > 0) return name;

        var code = (foodTypeCode ?? "").Trim();
        if (code.Length > 0 &&
            foodTypeNameMap.TryGetValue(code, out var masterName) &&
            !string.IsNullOrWhiteSpace(masterName))
            return masterName.Trim();

        return "";
    }

    private static string FormatEatingDate(string? yyyymmdd)
    {
        var s = (yyyymmdd ?? "").Trim();
        if (s.Length != 8) return s;
        return $"{s[..4]}/{s.Substring(4, 2)}/{s.Substring(6, 2)}";
    }

    private static Dictionary<string, string> BuildDetailTagValuesForPage(List<PersonalDeliveryLine> lines, int pageIndex)
    {
        var tagValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (lines.Count == 0) return tagValues;

        int start = pageIndex * RowsPerPage;
        var pageLines = lines.Skip(start).Take(RowsPerPage).ToList();
        if (pageLines.Count == 0) return tagValues;

        var first = lines[0];
        tagValues["DATE"] = first.EatingDate;
        tagValues["TIME"] = first.MealTimeName;
        tagValues["AREA"] = PersonalDeliveryHelper.FormatDeliveryAreaDisplay(first.Course);

        var headerRow = pageLines[0];
        tagValues["CUSTOMERNM"] = FormatCustomerName(headerRow);
        tagValues["CUSTOMERLOC"] = FormatCustomerLocation(headerRow);

        for (int i = 0; i < MaxRows; i++)
        {
            var nn = i.ToString("D2");
            var row = i < pageLines.Count ? pageLines[i] : null;
            tagValues[$"CUSTOMERNM{nn}"] = row != null ? FormatCustomerName(row) : "";
            tagValues[$"CUSTOMERLOC{nn}"] = row != null ? FormatCustomerLocation(row) : "";
            tagValues[$"FOODTYPE{nn}"] = row?.FoodType ?? "";
            tagValues[$"RICETYPE{nn}"] = row?.RiceType ?? "";
            tagValues[$"GRAM{nn}"] = row != null && row.HasRiceItem ? row.RiceAmount : "";
            tagValues[$"NOTE{nn}"] = row?.Remarks ?? "";
            tagValues[$"ORDER{nn}"] = row?.DeliveryOrder ?? "";
        }

        return tagValues;
    }

    private static Dictionary<string, string> BuildSummaryTagValues(List<PersonalDeliverySummaryLine> lines)
    {
        var tagValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (lines.Count == 0) return tagValues;

        var first = lines[0];
        tagValues["DATE"] = first.EatingDate;
        tagValues["TIME"] = first.MealTimeName;
        tagValues["AREA"] = PersonalDeliveryHelper.FormatDeliveryAreaDisplay(first.Course);
        tagValues["ORDER"] = "";

        for (int i = 0; i < MaxRows; i++)
        {
            var nn = i.ToString("D2");
            var row = i < lines.Count ? lines[i] : null;
            tagValues[$"FOODTYPE{nn}"] = row?.FoodType ?? "";
            tagValues[$"GRAM{nn}"] = row?.GramAmount ?? "";
            tagValues[$"COUNT{nn}"] = row != null
                ? row.TotalQuantity.ToString("0.##", CultureInfo.InvariantCulture)
                : "";
            tagValues[$"NOTE{nn}"] = row?.Note ?? "";
        }

        return tagValues;
    }

    private static bool TryParseCstmeatQuantity(string? info07, out decimal quantity)
    {
        quantity = 0;
        var s = (info07 ?? "").Trim();
        return s.Length > 0 &&
               decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out quantity);
    }

    private static string FormatCustomerName(PersonalDeliveryLine row)
    {
        var code = row.LocationCode ?? "";
        var name = row.LocationName ?? "";
        if (string.IsNullOrEmpty(code) && string.IsNullOrEmpty(name)) return "";
        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(name)) return code + name;
        return code + Environment.NewLine + name;
    }

    private static string FormatCustomerLocation(PersonalDeliveryLine row) =>
        (row.Address1 ?? row.CustomerAddress1 ?? "") + (row.Address2 ?? row.CustomerAddress2 ?? "");

    private sealed class PersonalDeliveryLine
    {
        public string EatingDate { get; set; } = "";
        public string MealTime { get; set; } = "";
        public string MealTimeName { get; set; } = "";
        public string Course { get; set; } = "";
        public string DeliveryOrder { get; set; } = "";
        public string LocationCode { get; set; } = "";
        public string LocationName { get; set; } = "";
        public string Address1 { get; set; } = "";
        public string Address2 { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public string CustomerAddress1 { get; set; } = "";
        public string CustomerAddress2 { get; set; } = "";
        public string FoodTypeCode { get; set; } = "";
        public string FoodType { get; set; } = "";
        public string RiceType { get; set; } = "";
        public string RiceAmount { get; set; } = "";
        public bool HasRiceItem { get; set; }
        public string Remarks { get; set; } = "";
    }

    private sealed class PersonalDeliverySummaryLine
    {
        public string EatingDate { get; set; } = "";
        public string MealTime { get; set; } = "";
        public string MealTimeName { get; set; } = "";
        public string Course { get; set; } = "";
        public PersonalDeliveryHelper.SummaryItemCategory Category { get; set; }
        public string FoodTypeCode { get; set; } = "";
        public string FoodType { get; set; } = "";
        public decimal TotalQuantity { get; set; }
        public string GramAmount { get; set; } = "";
        public string Note { get; set; } = "";
    }
}
