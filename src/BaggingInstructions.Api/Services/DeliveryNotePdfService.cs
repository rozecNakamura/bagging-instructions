using System.Globalization;
using BaggingInstructions.Api.Core;
using BaggingInstructions.Api.Entities;
using Microsoft.EntityFrameworkCore;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace BaggingInstructions.Api.Services;

/// <summary>
/// 納品書.rxz 用のタグ値を構築する。
/// 1枚に3帳票（_0, _1, _2）を縦並びで同一データを表示。明細が4行を超える場合は複数ページに分割。
/// 検索キー：cstmeat.info18（出荷日）。
/// ケータリング（200/210/220/230/240）: ITEMNM=食種名：喫食時間名、SUMCOUNT=食数合計、SUMPRICE=合計金額。
/// 個人配食（300）: ITEMNM=請求区分コード4文字目以降:喫食時間（朝/昼/夕）:info17。
/// 病院向（310）: 3011/3111/3411品目がある場合は ITEMNM=食種名:ご飯量(addinfo01)、なければ ITEMNM=食種名。
/// 個人配食（300）: 備考欄に受注明細のご飯品目（3010/3011/3111/3411）の addinfo01 を "{量}g" で印字。
/// </summary>
public class DeliveryNotePdfService
{
    private const int FormsPerSheet = 3;
    private const int ItemRowsPerForm = 4;
    private const string FullWidthColon = "：";
    private const string HalfWidthColon = ":";

    private static readonly IReadOnlySet<string> CateringCustomerCodes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "200", "210", "220", "230", "240" };

    private readonly CstmeatDbContext _cstmeatDb;
    private readonly AppDbContext _appDb;
    private readonly JuicePdfService _juicePdfService;

    public DeliveryNotePdfService(CstmeatDbContext cstmeatDb, AppDbContext appDb, JuicePdfService juicePdfService)
    {
        _cstmeatDb = cstmeatDb;
        _appDb = appDb;
        _juicePdfService = juicePdfService;
    }

    /// <summary>1件の納品書（出荷日・納入場所・得意先）に対する全ページ分のタグ値を構築する。明細が4行超の場合は複数ページ。</summary>
    public List<Dictionary<string, string>> BuildTagValuesPagesForOne(string eatingDateYyyymmdd, string locationCode, string customerCode, string deliveryRoute = "")
    {
        var pages = new List<Dictionary<string, string>>();
        var custCodeTrimmed = (customerCode ?? "").Trim();
        var customerType = GetCustomerType(custCodeTrimmed);

        // customerdeliverylocation（info02=locationcode かつ 得意先一致）＋ customer（craftlineax）
        var loc = ResolveLocation(locationCode ?? "", customerCode ?? "");
        var custCd = loc?.LocationCode ?? locationCode ?? "";
        var address1 = loc?.Address1 ?? "";
        var address2 = loc?.Address2 ?? "";
        var customerLoc = (address1 + (address2 ?? "")).Trim();
        var customerNm = loc?.LocationName ?? "";
        var customerTel = loc?.PhoneNumber ?? "";

        // info18: YYYYMMDD → YEAR, MONTH, DAY
        var year = "";
        var month = "";
        var day = "";
        if (!string.IsNullOrEmpty(eatingDateYyyymmdd) && eatingDateYyyymmdd.Length >= 8)
        {
            year = eatingDateYyyymmdd[..4];
            month = eatingDateYyyymmdd.Substring(4, 2);
            day = eatingDateYyyymmdd.Substring(6, 2);
        }

        // cstmeat を出荷日（info18）・納入場所（info02）・得意先（info01）・便（info19）で絞り込む
        var cstmeatQuery = _cstmeatDb.Cstmeats
            .AsNoTracking()
            .Where(c => c.Info18 == eatingDateYyyymmdd && (c.Info02 ?? "") == locationCode && (c.Info01 ?? "") == customerCode);
        if (!string.IsNullOrEmpty(deliveryRoute))
            cstmeatQuery = cstmeatQuery.Where(c => c.Info19 == deliveryRoute);
        var cstmeatRows = cstmeatQuery.ToList();

        // RECEPTNO: info03（喫食日 YYYYMMDD）→ salesorderline.planneddeliverydate でマッチして salesorderid を取得
        var info03Dates = cstmeatRows
            .Select(c => c.Info03)
            .Where(x => x != null && x.Length == 8)
            .Select(x => x!)
            .Distinct()
            .Select(s => DateOnly.TryParseExact(s, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var d) ? (DateOnly?)d : null)
            .Where(d => d.HasValue)
            .Select(d => d!.Value)
            .ToList();

        var salesOrderIds = new List<long>();
        if (info03Dates.Count > 0)
        {
            var locCodeTrimmed = (locationCode ?? "").Trim();
            var candidates = _appDb.SalesOrders
                .AsNoTracking()
                .Include(so => so.SalesOrderLines)
                .Where(so =>
                    so.CustomerCode == custCodeTrimmed &&
                    so.CustomerDeliveryLocationCode == locCodeTrimmed)
                .ToList();

            salesOrderIds = candidates
                .Where(so => so.SalesOrderLines.Any(l => l.PlannedDeliveryDate.HasValue && info03Dates.Contains(l.PlannedDeliveryDate!.Value)))
                .Select(so => so.SalesOrderId)
                .Distinct()
                .OrderBy(id => id)
                .ToList();
        }

        // 個人配食(300): 喫食時間(addinfo05)→ご飯量(addinfo01) のマップを構築
        var riceAmountByMealTime = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (customerType == "personal" && info03Dates.Count > 0)
        {
            var locCode = (locationCode ?? "").Trim();
            var riceLines = _appDb.SalesOrderLines
                .AsNoTracking()
                .Include(l => l.SalesOrder)
                .Include(l => l.Item)
                .Include(l => l.Addinfo)
                .Where(l =>
                    l.SalesOrder != null &&
                    l.SalesOrder.CustomerCode == custCodeTrimmed &&
                    l.SalesOrder.CustomerDeliveryLocationCode == locCode)
                .ToList()
                .Where(l => l.PlannedDeliveryDate.HasValue && info03Dates.Contains(l.PlannedDeliveryDate!.Value))
                .ToList();

            foreach (var line in riceLines)
            {
                if (!PersonalDeliveryHelper.IsRiceItemCode(line.Item?.ItemCd)) continue;
                var mealTime = (line.Addinfo?.Addinfo05 ?? "").Trim();
                var amount = (line.Addinfo?.Addinfo01 ?? "").Trim();
                if (mealTime.Length > 0 && amount.Length > 0 && !riceAmountByMealTime.ContainsKey(mealTime))
                    riceAmountByMealTime[mealTime] = amount;
            }
        }

        // info05 → m_shokushu.shokushu_name、info04 → eattime.eattimename
        var info05List = cstmeatRows.Select(c => c.Info05).Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList();
        var info04List = cstmeatRows.Select(c => c.Info04).Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList();

        var eattimeNameByCd = info04List.Count == 0
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : _cstmeatDb.Eattimes
                .AsNoTracking()
                .Where(e => info04List.Contains(e.Eattimecd ?? ""))
                .ToDictionary(e => e.Eattimecd ?? "", e => e.Eattimename ?? "", StringComparer.OrdinalIgnoreCase);

        // m_shokushu: 全区分で shokushu_name を取得、個人配食では seikyu_kubun_code も使用
        Dictionary<string, string> shokushuNameByCd = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> seikyuKubunByCd = new(StringComparer.OrdinalIgnoreCase);
        if (info05List.Count > 0)
        {
            var mshokushuList = _cstmeatDb.Mshokushus
                .AsNoTracking()
                .Where(m => m.ShokushuCode != null && info05List.Contains(m.ShokushuCode))
                .ToList();

            shokushuNameByCd = mshokushuList
                .GroupBy(m => m.ShokushuCode!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().ShokushuName ?? "", StringComparer.OrdinalIgnoreCase);

            if (customerType == "personal")
            {
                seikyuKubunByCd = mshokushuList
                    .Where(m => m.SeikyuKubunCode != null)
                    .GroupBy(m => m.ShokushuCode!, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First().SeikyuKubunCode ?? "", StringComparer.OrdinalIgnoreCase);
            }
        }

        // 個人配食: item_definition_master から単価コード名を取得（item_code='cdpret', item_def=info09 → item_name）
        Dictionary<string, string> itemDefNameByInfo09 = new(StringComparer.OrdinalIgnoreCase);
        if (customerType == "personal")
        {
            var info09List = cstmeatRows
                .Select(c => c.Info09)
                .Where(x => !string.IsNullOrEmpty(x))
                .Select(x => x!)
                .Distinct()
                .ToList();
            if (info09List.Count > 0)
            {
                itemDefNameByInfo09 = _cstmeatDb.ItemDefinitionMasters
                    .AsNoTracking()
                    .Where(d => d.ItemCode == "cdpret" && d.ItemDef != null && info09List.Contains(d.ItemDef))
                    .ToList()
                    .GroupBy(d => d.ItemDef!, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First().ItemName ?? "", StringComparer.OrdinalIgnoreCase);
            }
        }


        var grouped = cstmeatRows
            .GroupBy(c => new { Info05 = c.Info05 ?? "", Info04 = c.Info04 ?? "", Info06 = c.Info06 ?? "" })
            .Select(g =>
            {
                var info05 = g.Key.Info05;
                var info04 = g.Key.Info04;
                var info06 = g.Key.Info06;
                var info09 = g.Select(c => c.Info09).FirstOrDefault(v => !string.IsNullOrEmpty(v)) ?? "";
                var info17 = g.Select(c => c.Info17).FirstOrDefault(v => !string.IsNullOrEmpty(v)) ?? "";
                var cnt = g.Sum(x => decimal.TryParse(x.Info07, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0);
                var info08 = g.Select(c => c.Info08).FirstOrDefault(v => !string.IsNullOrEmpty(v)) ?? "";
                var unitPrice = decimal.TryParse(info08, NumberStyles.Any, CultureInfo.InvariantCulture, out var up) ? up : 0m;
                var price = g.Sum(x => decimal.TryParse(x.Info11, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0m);
                var info19 = g.Select(c => c.Info19).FirstOrDefault(v => !string.IsNullOrEmpty(v)) ?? "";
                var foodtypename = shokushuNameByCd.TryGetValue(info05, out var ft) ? ft : "";
                var eattimename = eattimeNameByCd.TryGetValue(info04, out var et) ? et : "";

                // 単価コード名: item_definition_master(cdpret, info09) → item_name のみ。マッチしない場合は空
                var tankaCdName = (!string.IsNullOrEmpty(info09) && itemDefNameByInfo09.TryGetValue(info09, out var defName))
                    ? defName
                    : "";

                var itemNm = BuildItemName(
                    customerType, info05, info04, foodtypename, eattimename,
                    tankaCdName, info17, info06, seikyuKubunByCd);

                var note = customerType == "personal" && riceAmountByMealTime.TryGetValue(info04, out var riceAmt)
                    ? $"{riceAmt}g"
                    : Info19Display(info19);

                return new
                {
                    ItemNm = itemNm,
                    Count = cnt,
                    UnitPrice = unitPrice,
                    Price = price,
                    Unit = "食",
                    Note = note
                };
            })
            .OrderBy(x => x.ItemNm)
            .ToList();

        var sumPriceTotal = grouped.Sum(x => x.Price);
        var sumCountTotal = grouped.Sum(x => x.Count);

        void SetHeaderTags(Dictionary<string, string> tags, string receptNo)
        {
            for (var f = 0; f < FormsPerSheet; f++)
            {
                tags[$"CUSTOMERCD_{f}"] = custCd;
                tags[$"CUSTOMERLOC_{f}"] = customerLoc;
                tags[$"CUSTOMERNM_{f}"] = customerNm;
                tags[$"CUSTOMERTEL_{f}"] = customerTel;
                tags[$"YEAR_{f}"] = year;
                tags[$"MONTH_{f}"] = month;
                tags[$"DAY_{f}"] = day;
                tags[$"RECEPTNO_{f}"] = receptNo;
                tags[$"SUMPRICE_{f}"] = sumPriceTotal != 0 ? sumPriceTotal.ToString(CultureInfo.InvariantCulture) : "";
                if (customerType == "catering")
                    tags[$"SUMCOUNT_{f}"] = sumCountTotal.ToString(CultureInfo.InvariantCulture);
            }
        }

        var totalItems = grouped.Count;
        var pageIndex = 0;
        for (var start = 0; start < totalItems; start += ItemRowsPerForm)
        {
            var receptNo = pageIndex < salesOrderIds.Count ? salesOrderIds[pageIndex].ToString() : "";
            var tagValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            SetHeaderTags(tagValues, receptNo);

            for (var i = 0; i < ItemRowsPerForm; i++)
            {
                var idx = start + i;
                var nn = i.ToString("D2");
                var row = idx < totalItems ? grouped[idx] : null;

                for (var f = 0; f < FormsPerSheet; f++)
                {
                    tagValues[$"ITEMNM_{f}_{nn}"] = row?.ItemNm ?? "";
                    tagValues[$"COUNT_{f}_{nn}"] = row != null ? row.Count.ToString(CultureInfo.InvariantCulture) : "";
                    tagValues[$"UNIT_{f}_{nn}"] = row?.Unit ?? "";
                    tagValues[$"UNITPRICE_{f}_{nn}"] = row != null && row.UnitPrice != 0 ? row.UnitPrice.ToString(CultureInfo.InvariantCulture) : "";
                    tagValues[$"PRICE_{f}_{nn}"] = row != null && row.Price != 0 ? row.Price.ToString(CultureInfo.InvariantCulture) : "";
                    tagValues[$"NOTE_{f}_{nn}"] = row?.Note ?? "";
                }
            }

            pages.Add(tagValues);
            pageIndex++;
        }

        // 明細なしでも1ページ出力
        if (pages.Count == 0)
        {
            var receptNo = salesOrderIds.Count > 0 ? salesOrderIds[0].ToString() : "";
            var tagValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            SetHeaderTags(tagValues, receptNo);
            for (var f = 0; f < FormsPerSheet; f++)
            {
                for (var i = 0; i < ItemRowsPerForm; i++)
                {
                    var nn = i.ToString("D2");
                    tagValues[$"ITEMNM_{f}_{nn}"] = "";
                    tagValues[$"COUNT_{f}_{nn}"] = "";
                    tagValues[$"UNIT_{f}_{nn}"] = "";
                    tagValues[$"UNITPRICE_{f}_{nn}"] = "";
                    tagValues[$"PRICE_{f}_{nn}"] = "";
                    tagValues[$"NOTE_{f}_{nn}"] = "";
                }
            }
            pages.Add(tagValues);
        }

        return pages;
    }

    private static string GetCustomerType(string customerCode) =>
        CateringCustomerCodes.Contains(customerCode) ? "catering" :
        customerCode == "300" ? "personal" :
        customerCode == "310" ? "hospital" :
        "catering";

    private static string BuildItemName(
        string customerType,
        string info05,
        string info04,
        string foodtypename,
        string eattimename,
        string tankaCdName,
        string kinshiShokuzai,
        string info06,
        Dictionary<string, string> seikyuKubunByCd)
    {
        switch (customerType)
        {
            case "personal":
            {
                // 請求区分名称（seikyu_kubun_code 4文字目以降）: 喫食時間 : 単価コード名 : 禁止食材(info17)
                seikyuKubunByCd.TryGetValue(info05, out var kubun);
                var kubunSuffix = !string.IsNullOrEmpty(kubun)
                    ? (kubun.Length >= 4 ? kubun[3..] : kubun)
                    : "";
                var mealDisplay = MealTimeDisplay(info04);
                var parts = new List<string>();
                if (!string.IsNullOrEmpty(kubunSuffix)) parts.Add(kubunSuffix);
                if (!string.IsNullOrEmpty(mealDisplay)) parts.Add(mealDisplay);
                if (!string.IsNullOrEmpty(tankaCdName)) parts.Add(tankaCdName);
                if (!string.IsNullOrEmpty(kinshiShokuzai)) parts.Add(kinshiShokuzai);
                return string.Join(HalfWidthColon, parts);
            }
            default: // ケータリング・病院向: 食種名称:喫食時間:info06変換値
            {
                var parts = new List<string>();
                var food = (foodtypename ?? "").Trim();
                if (!string.IsNullOrEmpty(food)) parts.Add(food);
                var eattime = (eattimename ?? "").Trim();
                if (!string.IsNullOrEmpty(eattime)) parts.Add(eattime);
                var info06Display = Info06Display(info06);
                if (!string.IsNullOrEmpty(info06Display)) parts.Add(info06Display);
                return string.Join(HalfWidthColon, parts);
            }
        }
    }

    private static string Info06Display(string? info06) =>
        (info06 ?? "").Trim() switch
        {
            "1" => "通常品",
            "2" => "検食",
            "3" => "検体",
            var s => s
        };

    private static string Info19Display(string? info19) =>
        (info19 ?? "").Trim() switch
        {
            "1" => "出荷便朝",
            "2" => "出荷便昼",
            "3" => "出荷便夕",
            var s => s
        };

    private static string MealTimeDisplay(string? info04) =>
        (info04 ?? "").Trim() switch
        {
            "1" => "朝",
            "2" => "昼",
            "3" => "夕",
            var s => s
        };

    private CustomerDeliveryLocation? ResolveLocation(string locationCode, string customerCode)
    {
        var locs = _appDb.CustomerDeliveryLocations
            .AsNoTracking()
            .Include(l => l.Customer)
            .Where(l => l.LocationCode != null && l.Customer != null)
            .ToList();

        var locCode = (locationCode ?? "").Trim();
        var custCode = (customerCode ?? "").Trim();
        var locCodeNorm = NormalizeCode(locCode);
        var custCodeNorm = NormalizeCode(custCode);

        foreach (var l in locs)
        {
            var lc = (l.LocationCode ?? "").Trim();
            var cc = (l.Customer?.CustomerCode ?? "").Trim();
            if (string.IsNullOrEmpty(lc)) continue;
            if (lc.Equals(locCode, StringComparison.OrdinalIgnoreCase) || lc == locCodeNorm)
            {
                if (cc.Equals(custCode, StringComparison.OrdinalIgnoreCase) || cc == custCodeNorm ||
                    (string.IsNullOrEmpty(custCode) && string.IsNullOrEmpty(cc)))
                    return l;
            }
        }
        return locs.FirstOrDefault(l => (l.LocationCode ?? "").Trim().Equals(locCode, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeCode(string? s)
    {
        var t = (s ?? "").Trim();
        if (t.Length == 0) return "";
        var trimmed = t.TrimStart('0');
        return trimmed.Length == 0 ? "0" : trimmed;
    }

    /// <summary>複数件の納品書を1つのPDFに結合して返す。1件あたり1～複数ページ（明細4行超で複数ページ）。</summary>
    public byte[] GenerateMergedPdf(string rxzTemplatePath, IReadOnlyList<(string EatingDate, string LocationCode, string CustomerCode, string DeliveryRoute)> rows)
    {
        if (rows == null || rows.Count == 0)
            return Array.Empty<byte>();

        var outputDoc = new PdfDocument();
        foreach (var (eatingDate, locationCode, customerCode, deliveryRoute) in rows)
        {
            var pageTagLists = BuildTagValuesPagesForOne(eatingDate, locationCode, customerCode, deliveryRoute);
            foreach (var tagValues in pageTagLists)
            {
                var onePdf = _juicePdfService.GeneratePdf(rxzTemplatePath, tagValues);
                using var ms = new MemoryStream(onePdf);
                var doc = PdfReader.Open(ms, PdfDocumentOpenMode.Import);
                for (var i = 0; i < doc.PageCount; i++)
                    outputDoc.AddPage(doc.Pages[i]);
            }
        }
        using var outMs = new MemoryStream();
        outputDoc.Save(outMs, false);
        return outMs.ToArray();
    }
}
