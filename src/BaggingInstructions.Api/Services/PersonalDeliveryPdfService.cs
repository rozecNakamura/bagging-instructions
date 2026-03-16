using System.Globalization;
using BaggingInstructions.Api.Core;
using BaggingInstructions.Api.Entities;
using Microsoft.EntityFrameworkCore;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace BaggingInstructions.Api.Services;

/// <summary>
/// 個人配送指示書.rxz / 個人配送指示書（集計）.rxz 用の PDF 生成。
/// 1枚目: 個人配送指示書.rxz（明細）、2枚目: 個人配送指示書（集計）.rxz（集計）。
/// </summary>
public class PersonalDeliveryPdfService
{
    /// <summary>テンプレート1ページあたりのデータ行数。これを超える分は次ページへ。</summary>
    private const int RowsPerPage = 22;
    private const int MaxRows = 40;
    private readonly AppDbContext _appDb;
    private readonly JuicePdfService _juicePdfService;

    public PersonalDeliveryPdfService(AppDbContext appDb, JuicePdfService juicePdfService)
    {
        _appDb = appDb;
        _juicePdfService = juicePdfService;
    }

    /// <summary>選択された (配送日, 喫食時間, 配送エリア) ごとにPDFを生成。variant="detail" は明細のみ、"summary" は集計のみ。</summary>
    public async Task<byte[]> GeneratePdfAsync(
        string templateDetailPath,
        string templateSummaryPath,
        IReadOnlyList<(string DeliveryDate, string TimeName, string Area)> selections,
        string variant,
        CancellationToken ct = default)
    {
        if (selections == null || selections.Count == 0)
            return Array.Empty<byte>();

        var outputDoc = new PdfDocument();
        var isDetail = string.Equals(variant, "detail", StringComparison.OrdinalIgnoreCase);
        var isSummary = string.Equals(variant, "summary", StringComparison.OrdinalIgnoreCase);

        foreach (var (deliveryDate, timeName, area) in selections)
        {
            var lines = await LoadLines(deliveryDate, timeName, area, ct);
            if (lines.Count == 0) continue;

            if (isDetail)
            {
                // 22行ごとにページ分割し、23行目以降は2ページ目以降に表示
                for (int pageIndex = 0; pageIndex * RowsPerPage < lines.Count; pageIndex++)
                {
                    var tagDetail = BuildDetailTagValuesForPage(lines, pageIndex);
                    var pdfPage = _juicePdfService.GeneratePdf(templateDetailPath, tagDetail);
                    AppendPdf(outputDoc, pdfPage);
                }
            }
            else if (isSummary)
            {
                var tagSummary = BuildSummaryTagValues(lines);
                var pdf2 = _juicePdfService.GeneratePdf(templateSummaryPath, tagSummary);
                AppendPdf(outputDoc, pdf2);
            }
        }

        using var ms = new MemoryStream();
        outputDoc.Save(ms, false);
        return ms.ToArray();
    }

    private async Task<List<PersonalDeliveryLine>> LoadLines(string deliveryDate, string timeName, string area, CancellationToken ct = default)
    {
        if (!DateOnly.TryParseExact(deliveryDate, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var date))
            return new List<PersonalDeliveryLine>();

        var query = _appDb.SalesOrderLines
            .AsNoTracking()
            .Include(l => l.Addinfo)
            .Include(l => l.SalesOrder!)
                .ThenInclude(so => so!.CustomerDeliveryLocation)
                    .ThenInclude(loc => loc!.Addinfo)
            .Include(l => l.SalesOrder!)
                .ThenInclude(so => so!.Customer)
            .Include(l => l.Item!)
                .ThenInclude(i => i!.AdditionalInformation)
            .Where(l => l.PlannedDeliveryDate == date);

        if (!string.IsNullOrEmpty(timeName))
            query = query.Where(l => l.Addinfo != null && l.Addinfo.Addinfo01Name == timeName);
        if (!string.IsNullOrEmpty(area))
            query = query.Where(l => l.SalesOrder != null && l.SalesOrder.CustomerDeliveryLocation != null
                && l.SalesOrder.CustomerDeliveryLocation.Addinfo != null
                && (l.SalesOrder.CustomerDeliveryLocation.Addinfo.Addinfo01 ?? "") == area);

        var list = await query.OrderBy(l => l.SalesOrderLineId).ToListAsync(ct);
        return list.Select(l => ToPersonalDeliveryLine(l)).ToList();
    }

    private static PersonalDeliveryLine ToPersonalDeliveryLine(SalesOrderLine l)
    {
        var so = l.SalesOrder;
        var loc = so?.CustomerDeliveryLocation;
        var customer = so?.Customer;
        var addinfo = l.Addinfo;
        var itemAddinfo = l.Item?.AdditionalInformation;
        return new PersonalDeliveryLine
        {
            PlannedDeliveryDate = l.PlannedDeliveryDate?.ToString("yyyy/MM/dd") ?? "",
            TimeName = addinfo?.Addinfo01Name ?? "",
            Area = loc?.Addinfo?.Addinfo01 ?? "",
            LocationCode = loc?.LocationCode ?? "",
            LocationName = loc?.LocationName ?? "",
            Address1 = loc?.Address1 ?? "",
            Address2 = loc?.Address2 ?? "",
            CustomerName = customer?.CustomerName ?? "",
            CustomerAddress1 = customer?.Address1 ?? "",
            CustomerAddress2 = customer?.Address2 ?? "",
            FoodType = addinfo?.Addinfo03Name ?? "",
            RiceType = itemAddinfo?.Addinfo02 ?? "",
            Quantity = l.Quantity,
            Remarks = l.Remarks ?? "",
            Addinfo02Divisor = ParseDivisor(addinfo?.Addinfo02)
        };
    }

    private static decimal? ParseDivisor(string? addinfo02)
    {
        if (string.IsNullOrWhiteSpace(addinfo02)) return null;
        if (decimal.TryParse(addinfo02.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) && v != 0)
            return v;
        return null;
    }

    /// <summary>個人配送指示書.rxz 用タグ: DATE, TIME, AREA, CUSTOMERNM, CUSTOMERLOC, CUSTOMERNM00-39, CUSTOMERLOC00-39, FOODTYPE00-39, RICETYPE00-39, GRAM00-39, NOTE00-39, ORDER00-39。1ページあたり RowsPerPage 行まで。指定ページ分の行を 00～21 に詰め、残りは空。</summary>
    private static Dictionary<string, string> BuildDetailTagValuesForPage(List<PersonalDeliveryLine> lines, int pageIndex)
    {
        var tagValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (lines.Count == 0) return tagValues;

        int start = pageIndex * RowsPerPage;
        var pageLines = lines.Skip(start).Take(RowsPerPage).ToList();
        if (pageLines.Count == 0) return tagValues;

        var first = lines[0];
        tagValues["DATE"] = first.PlannedDeliveryDate;
        tagValues["TIME"] = first.TimeName;
        tagValues["AREA"] = first.Area;
        // ヘッダー用: 当該ページの先頭行の顧客名・住所
        var headerRow = pageLines[0];
        var customerNmFirst = (headerRow.LocationCode ?? "") + (string.IsNullOrEmpty(headerRow.LocationCode) && string.IsNullOrEmpty(headerRow.LocationName) ? "" : Environment.NewLine) + (headerRow.LocationName ?? "");
        var customerLocFirst = (headerRow.Address1 ?? headerRow.CustomerAddress1 ?? "") + (headerRow.Address2 ?? headerRow.CustomerAddress2 ?? "");
        tagValues["CUSTOMERNM"] = customerNmFirst;
        tagValues["CUSTOMERLOC"] = customerLocFirst;

        for (int i = 0; i < MaxRows; i++)
        {
            var nn = i.ToString("D2");
            var row = i < pageLines.Count ? pageLines[i] : null;
            var customerNm = row != null
                ? (row.LocationCode ?? "") + (string.IsNullOrEmpty(row.LocationCode) && string.IsNullOrEmpty(row.LocationName) ? "" : Environment.NewLine) + (row.LocationName ?? "")
                : "";
            var customerLoc = row != null
                ? (row.Address1 ?? row.CustomerAddress1 ?? "") + (row.Address2 ?? row.CustomerAddress2 ?? "")
                : "";
            tagValues[$"CUSTOMERNM{nn}"] = customerNm;
            tagValues[$"CUSTOMERLOC{nn}"] = customerLoc;
            tagValues[$"FOODTYPE{nn}"] = row?.FoodType ?? "";
            tagValues[$"RICETYPE{nn}"] = row?.RiceType ?? "";
            tagValues[$"GRAM{nn}"] = row != null ? row.Quantity.ToString(CultureInfo.InvariantCulture) : "";
            tagValues[$"NOTE{nn}"] = row?.Remarks ?? "";
            tagValues[$"ORDER{nn}"] = row != null ? "0" : "";
        }

        return tagValues;
    }

    /// <summary>個人配送指示書（集計）.rxz 用タグ: DATE, TIME, AREA, ORDER→なし, FOODTYPE00-39, GRAM00-39, COUNT00-39（quantity/addinfo02 を addinfo03name で集計）, NOTE00-39</summary>
    private static Dictionary<string, string> BuildSummaryTagValues(List<PersonalDeliveryLine> lines)
    {
        var tagValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (lines.Count == 0) return tagValues;

        var first = lines[0];
        tagValues["DATE"] = first.PlannedDeliveryDate;
        tagValues["TIME"] = first.TimeName;
        tagValues["AREA"] = first.Area;
        tagValues["ORDER"] = "";

        // addinfo03name (FOODTYPE) でグループ化し、COUNT = sum(quantity / addinfo02)
        var grouped = lines
            .GroupBy(l => l.FoodType ?? "")
            .Select(g => new
            {
                FoodType = g.Key,
                Gram = g.Sum(x => x.Quantity),
                Count = g.Sum(x => x.Addinfo02Divisor.HasValue && x.Addinfo02Divisor.Value != 0
                    ? x.Quantity / x.Addinfo02Divisor.Value
                    : 0),
                Note = string.Join(" ", g.Select(x => x.Remarks).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct())
            })
            .OrderBy(x => x.FoodType)
            .ToList();

        for (int i = 0; i < MaxRows; i++)
        {
            var nn = i.ToString("D2");
            var row = i < grouped.Count ? grouped[i] : null;
            tagValues[$"FOODTYPE{nn}"] = row?.FoodType ?? "";
            tagValues[$"GRAM{nn}"] = row != null ? row.Gram.ToString(CultureInfo.InvariantCulture) : "";
            tagValues[$"COUNT{nn}"] = row != null ? row.Count.ToString(CultureInfo.InvariantCulture) : "";
            tagValues[$"NOTE{nn}"] = row?.Note ?? "";
        }

        return tagValues;
    }

    private static void AppendPdf(PdfDocument outputDoc, byte[] pdfBytes)
    {
        using var ms = new MemoryStream(pdfBytes);
        var doc = PdfReader.Open(ms, PdfDocumentOpenMode.Import);
        for (int i = 0; i < doc.PageCount; i++)
            outputDoc.AddPage(doc.Pages[i]);
    }

    private class PersonalDeliveryLine
    {
        public string PlannedDeliveryDate { get; set; } = "";
        public string TimeName { get; set; } = "";
        public string Area { get; set; } = "";
        public string LocationCode { get; set; } = "";
        public string LocationName { get; set; } = "";
        public string Address1 { get; set; } = "";
        public string Address2 { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public string CustomerAddress1 { get; set; } = "";
        public string CustomerAddress2 { get; set; } = "";
        public string FoodType { get; set; } = "";
        public string RiceType { get; set; } = "";
        public decimal Quantity { get; set; }
        public string Remarks { get; set; } = "";
        public decimal? Addinfo02Divisor { get; set; }
    }
}
