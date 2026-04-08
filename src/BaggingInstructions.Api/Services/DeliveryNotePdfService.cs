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
/// CUSTOMERCD→customerdeliverylocation.customerid, CUSTOMERLOC→address1+address2, CUSTOMERNM→customer.customername,
/// CUSTOMERTEL→customerdeliverylocation.phonenumber, YEAR/MONTH/DAY→cstmeat.info03,
/// ITEMNM→foodtype.foodtypename＋全角コロン＋eattime.eattimename（info05↔foodtypecd, info04↔eattimecdで連携、いずれもcraftlineaxother）, COUNT→info07合算（info05+info04でグループ化）。
/// </summary>
public class DeliveryNotePdfService
{
    private const int FormsPerSheet = 3;
    private const int ItemRowsPerForm = 4;

    private readonly CstmeatDbContext _cstmeatDb;
    private readonly AppDbContext _appDb;
    private readonly JuicePdfService _juicePdfService;

    public DeliveryNotePdfService(CstmeatDbContext cstmeatDb, AppDbContext appDb, JuicePdfService juicePdfService)
    {
        _cstmeatDb = cstmeatDb;
        _appDb = appDb;
        _juicePdfService = juicePdfService;
    }

    /// <summary>1件の納品書（喫食日・納入場所・得意先）に対する全ページ分のタグ値を構築する。明細が4行超の場合は複数ページ。</summary>
    public List<Dictionary<string, string>> BuildTagValuesPagesForOne(string eatingDateYyyymmdd, string locationCode, string customerCode)
    {
        var pages = new List<Dictionary<string, string>>();

        // customerdeliverylocation（info02=locationcode かつ 得意先一致）＋ customer（craftlineax）
        var loc = ResolveLocation(locationCode, customerCode);
        var customer = loc?.Customer;
        var custCd = customer?.CustomerCode ?? customerCode ?? "";
        var address1 = loc?.Address1 ?? "";
        var address2 = loc?.Address2 ?? "";
        var customerLoc = (address1 + (address2 ?? "")).Trim();
        var customerNm = customer?.CustomerName ?? "";
        var customerTel = loc?.PhoneNumber ?? "";

        // info03: YYYYMMDD → YEAR, MONTH, DAY
        var year = "";
        var month = "";
        var day = "";
        if (!string.IsNullOrEmpty(eatingDateYyyymmdd) && eatingDateYyyymmdd.Length >= 8)
        {
            year = eatingDateYyyymmdd.Length >= 4 ? eatingDateYyyymmdd[..4] : "";
            month = eatingDateYyyymmdd.Length >= 6 ? eatingDateYyyymmdd.Substring(4, 2) : "";
            day = eatingDateYyyymmdd.Length >= 8 ? eatingDateYyyymmdd.Substring(6, 2) : "";
        }

        // cstmeat から (info03, info02, info01) で該当行を取得し、info05+info04 でグループ化、COUNT=sum(info07)
        var cstmeatRows = _cstmeatDb.Cstmeats
            .AsNoTracking()
            .Where(c => c.Info03 == eatingDateYyyymmdd && (c.Info02 ?? "") == locationCode && (c.Info01 ?? "") == customerCode)
            .ToList();

        // info05 → foodtype.foodtypename、info04 → eattime.eattimename（いずれも craftlineaxother、info04 と eattimecd で結合）
        var info05List = cstmeatRows.Select(c => c.Info05).Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList();
        var info04List = cstmeatRows.Select(c => c.Info04).Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList();

        var foodtypeNameByCd = info05List.Count == 0
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : _cstmeatDb.Foodtypes
                .AsNoTracking()
                .Where(f => info05List.Contains(f.Foodtypecd ?? ""))
                .ToDictionary(f => f.Foodtypecd ?? "", f => f.Foodtypename ?? "", StringComparer.OrdinalIgnoreCase);

        var eattimeNameByCd = info04List.Count == 0
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : _cstmeatDb.Eattimes
                .AsNoTracking()
                .Where(e => info04List.Contains(e.Eattimecd ?? ""))
                .ToDictionary(e => e.Eattimecd ?? "", e => e.Eattimename ?? "", StringComparer.OrdinalIgnoreCase);

        var itemCdList = info05List;
        var itemsByCd = itemCdList.Count == 0
            ? new Dictionary<string, Item>()
            : _appDb.Items
                .AsNoTracking()
                .Include(i => i.Unit0)
                .Where(i => itemCdList.Contains(i.ItemCd ?? ""))
                .ToDictionary(i => i.ItemCd ?? "", i => i);

        const string itemNmSeparator = "："; // 全角コロン

        var grouped = cstmeatRows
            .GroupBy(c => new { Info05 = c.Info05 ?? "", Info04 = c.Info04 ?? "" })
            .Select(g =>
            {
                var itemCd = g.Key.Info05;
                itemsByCd.TryGetValue(itemCd ?? "", out var it);
                var unitPrice = it != null && it.SalesPrice0.HasValue ? it.SalesPrice0.Value : 0m;
                var cnt = g.Sum(x => decimal.TryParse(x.Info07, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0);
                var unit = it?.Unit0?.UnitCode ?? "";
                var foodtypename = foodtypeNameByCd.TryGetValue(g.Key.Info05, out var ft) ? ft : "";
                var eattimename = eattimeNameByCd.TryGetValue(g.Key.Info04, out var et) ? et : "";
                var itemNm = (foodtypename ?? "").TrimEnd() + itemNmSeparator + (eattimename ?? "").TrimStart();
                return new
                {
                    ItemNm = itemNm,
                    Count = cnt,
                    ItemCd = itemCd,
                    UnitPrice = unitPrice,
                    Price = cnt * unitPrice,
                    Unit = unit
                };
            })
            .OrderBy(x => x.ItemNm)
            .ToList();

        decimal sumPriceTotal = grouped.Sum(x => x.Price);

        // 共通ヘッダー（3帳票で同じ値）
        void SetHeaderTags(Dictionary<string, string> tags)
        {
            for (int f = 0; f < FormsPerSheet; f++)
            {
                tags[$"CUSTOMERCD_{f}"] = custCd;
                tags[$"CUSTOMERLOC_{f}"] = customerLoc;
                tags[$"CUSTOMERNM_{f}"] = customerNm;
                tags[$"CUSTOMERTEL_{f}"] = customerTel;
                tags[$"YEAR_{f}"] = year;
                tags[$"MONTH_{f}"] = month;
                tags[$"DAY_{f}"] = day;
                tags[$"RECEPTNO_{f}"] = "";
                tags[$"SUMPRICE_{f}"] = sumPriceTotal.ToString(CultureInfo.InvariantCulture);
            }
        }

        // 1ページあたり ItemRowsPerForm 行。複数ページに分割
        int totalItems = grouped.Count;
        int pageIndex = 0;
        for (int start = 0; start < totalItems; start += ItemRowsPerForm, pageIndex++)
        {
            var tagValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            SetHeaderTags(tagValues);

            int end = Math.Min(start + ItemRowsPerForm, totalItems);
            decimal pageSum = 0;
            for (int i = 0; i < ItemRowsPerForm; i++)
            {
                int idx = start + i;
                var nn = i.ToString("D2");
                var row = idx < totalItems ? grouped[idx] : null;

                for (int f = 0; f < FormsPerSheet; f++)
                {
                    tagValues[$"ITEMNM_{f}_{nn}"] = row?.ItemNm ?? "";
                    tagValues[$"COUNT_{f}_{nn}"] = row != null ? row.Count.ToString(CultureInfo.InvariantCulture) : "";
                    tagValues[$"UNIT_{f}_{nn}"] = row?.Unit ?? "";
                    tagValues[$"UNITPRICE_{f}_{nn}"] = row != null ? row.UnitPrice.ToString(CultureInfo.InvariantCulture) : "";
                    tagValues[$"PRICE_{f}_{nn}"] = row != null ? row.Price.ToString(CultureInfo.InvariantCulture) : "";
                    tagValues[$"NOTE_{f}_{nn}"] = "";
                }
                if (row != null)
                    pageSum += row.Price;
            }

            pages.Add(tagValues);
        }

        // 1件も明細がない場合でも1ページは出す
        if (pages.Count == 0)
        {
            var tagValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            SetHeaderTags(tagValues);
            for (int f = 0; f < FormsPerSheet; f++)
            {
                for (int i = 0; i < ItemRowsPerForm; i++)
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
        // 納入場所コードのみでフォールバック（既存挙動）
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
    public byte[] GenerateMergedPdf(string rxzTemplatePath, IReadOnlyList<(string EatingDate, string LocationCode, string CustomerCode)> rows)
    {
        if (rows == null || rows.Count == 0)
            return Array.Empty<byte>();

        var outputDoc = new PdfDocument();
        foreach (var (eatingDate, locationCode, customerCode) in rows)
        {
            var pageTagLists = BuildTagValuesPagesForOne(eatingDate, locationCode, customerCode);
            foreach (var tagValues in pageTagLists)
            {
                var onePdf = _juicePdfService.GeneratePdf(rxzTemplatePath, tagValues);
                using var ms = new MemoryStream(onePdf);
                var doc = PdfReader.Open(ms, PdfDocumentOpenMode.Import);
                for (int i = 0; i < doc.PageCount; i++)
                    outputDoc.AddPage(doc.Pages[i]);
            }
        }
        using var outMs = new MemoryStream();
        outputDoc.Save(outMs, false);
        return outMs.ToArray();
    }
}
