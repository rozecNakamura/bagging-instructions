using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using BaggingInstructions.Api.Core;
using BaggingInstructions.Api.DTOs;

namespace BaggingInstructions.Api.Services;

public sealed class AggregateSummaryService
{
    private readonly AppDbContext _db;
    private readonly PreparationWorkService _preparationWorkService;

    public AggregateSummaryService(AppDbContext db, PreparationWorkService preparationWorkService)
    {
        _db = db;
        _preparationWorkService = preparationWorkService;
    }

    public async Task<List<AggregateSummaryRowDto>> SearchSummaryAsync(
        string fromDate,
        string? toDate,
        string? itemCode,
        string? majorClass,
        string? middleClass,
        CancellationToken ct = default)
    {
        var from = ParseYyyymmdd(fromDate);
        if (!from.HasValue)
            throw new ArgumentException("from_date は YYYYMMDD 形式（8桁）で指定してください。", nameof(fromDate));

        DateOnly to;
        if (string.IsNullOrWhiteSpace(toDate))
        {
            to = from.Value;
        }
        else
        {
            var toParsed = ParseYyyymmdd(toDate);
            if (!toParsed.HasValue)
                throw new ArgumentException("to_date は YYYYMMDD 形式（8桁）で指定してください。", nameof(toDate));
            if (toParsed.Value < from.Value)
                throw new ArgumentException("to_date は from_date 以上の日付を指定してください。", nameof(toDate));
            to = toParsed.Value;
        }

        var itemF = itemCode?.Trim() ?? "";
        var majorF = majorClass?.Trim() ?? "";
        var middleF = middleClass?.Trim() ?? "";

        var rows = await _db.Database
            .SqlQuery<AggregateSummarySqlRow>($@"
SELECT
  TO_CHAR(need_date, 'YYYYMMDD') AS ""ShipDate"",
  COALESCE(mc.majorclassificationcode, '') AS ""MajorCode"",
  COALESCE(mc.majorclassificationname, '') AS ""MajorName"",
  COALESCE(mid.middleclassificationcode, '') AS ""MiddleCode"",
  COALESCE(mid.middleclassificationname, '') AS ""MiddleName"",
  COUNT(DISTINCT b.childitemcode)::int AS ""ChildItemCount""
FROM (
  SELECT
    sol.salesorderlineid,
    COALESCE(
      (SELECT ot.needdate FROM ordertable ot WHERE ot.salesorderlineid = sol.salesorderlineid ORDER BY ot.ordertableid LIMIT 1),
      sol.planneddeliverydate
    ) AS need_date,
    i.itemcode AS parent_itemcode
  FROM salesorderline sol
  INNER JOIN item i ON i.itemcode = sol.itemcode
) base
INNER JOIN item i ON i.itemcode = base.parent_itemcode
LEFT JOIN majorclassification mc ON mc.majorclassificationcode = i.majorclassficationcode
LEFT JOIN middleclassification mid ON mid.majorclassificationcode = i.majorclassficationcode
  AND mid.middleclassificationcode = i.middleclassficationcode
LEFT JOIN bom b ON b.parentitemcode = base.parent_itemcode
WHERE base.need_date BETWEEN {from.Value} AND {to}
  AND ({itemF} = '' OR i.itemcode ILIKE '%' || {itemF} || '%')
  AND ({majorF} = '' OR mc.majorclassificationcode = {majorF})
  AND ({middleF} = '' OR mid.middleclassificationcode = {middleF})
GROUP BY
  TO_CHAR(need_date, 'YYYYMMDD'),
  mc.majorclassificationcode,
  mc.majorclassificationname,
  mid.middleclassificationcode,
  mid.middleclassificationname
ORDER BY ""ShipDate"", ""MajorCode"", ""MiddleCode""
")
            .ToListAsync(ct);

        return rows.Select(r => new AggregateSummaryRowDto
        {
            ShipDate = FormatDateDisplay(r.ShipDate),
            MajorClassificationName = r.MajorName,
            MiddleClassificationName = r.MiddleName,
            ChildItemCount = r.ChildItemCount,
            Key = new AggregateSummaryKeyDto
            {
                ShipDate = r.ShipDate,
                MajorClassificationCode = string.IsNullOrEmpty(r.MajorCode) ? null : r.MajorCode,
                MiddleClassificationCode = string.IsNullOrEmpty(r.MiddleCode) ? null : r.MiddleCode
            }
        }).ToList();
    }

    public async Task<IReadOnlyList<long>> ResolveLineIdsForSummaryAsync(
        AggregateSummaryReportFilterDto filter,
        IReadOnlyList<AggregateSummaryKeyDto> keys,
        CancellationToken ct = default)
    {
        if (keys == null || keys.Count == 0)
            return Array.Empty<long>();

        var from = ParseYyyymmdd(filter.FromDate ?? "");
        if (!from.HasValue)
            throw new ArgumentException("from_date は YYYYMMDD 形式（8桁）で指定してください。", nameof(filter.FromDate));

        DateOnly to;
        if (string.IsNullOrWhiteSpace(filter.ToDate))
        {
            to = from.Value;
        }
        else
        {
            var toParsed = ParseYyyymmdd(filter.ToDate);
            if (!toParsed.HasValue)
                throw new ArgumentException("to_date は YYYYMMDD 形式（8桁）で指定してください。", nameof(filter.ToDate));
            to = toParsed.Value;
        }

        var itemF = filter.ItemCode?.Trim() ?? "";

        var all = new HashSet<long>();
        foreach (var key in keys)
        {
            var ship = ParseYyyymmdd(key.ShipDate);
            if (!ship.HasValue)
                continue;

            var maj = key.MajorClassificationCode ?? "";
            var mid = key.MiddleClassificationCode ?? "";

            var list = await _db.Database
                .SqlQuery<AggregateSummaryLineIdRow>($@"
SELECT sol.salesorderlineid AS ""SalesOrderLineId""
FROM salesorderline sol
INNER JOIN item i ON i.itemcode = sol.itemcode
LEFT JOIN majorclassification mc ON mc.majorclassificationcode = i.majorclassficationcode
LEFT JOIN middleclassification midt ON midt.majorclassificationcode = i.majorclassficationcode
  AND midt.middleclassificationcode = i.middleclassficationcode
LEFT JOIN ordertable ot ON ot.salesorderlineid = sol.salesorderlineid
WHERE
  COALESCE(
    (SELECT ot2.needdate FROM ordertable ot2 WHERE ot2.salesorderlineid = sol.salesorderlineid ORDER BY ot2.ordertableid LIMIT 1),
    sol.planneddeliverydate
  ) BETWEEN {from.Value} AND {to}
  AND TO_CHAR(
    COALESCE(
      (SELECT ot3.needdate FROM ordertable ot3 WHERE ot3.salesorderlineid = sol.salesorderlineid ORDER BY ot3.ordertableid LIMIT 1),
      sol.planneddeliverydate
    ),
    'YYYYMMDD'
  ) = {key.ShipDate}
  AND ({itemF} = '' OR i.itemcode ILIKE '%' || {itemF} || '%')
  AND ({maj} = '' OR COALESCE(mc.majorclassificationcode, '') = {maj})
  AND ({mid} = '' OR COALESCE(midt.middleclassificationcode, '') = {mid})
")
                .ToListAsync(ct);

            foreach (var row in list)
                all.Add(row.SalesOrderLineId);
        }

        return all.OrderBy(x => x).ToList();
    }

    public async Task<List<AggregateSummaryPdfLineModel>> BuildPdfLineModelsForSummaryAsync(
        AggregateSummaryReportFilterDto filter,
        IReadOnlyList<AggregateSummaryKeyDto> keys,
        CancellationToken ct = default)
    {
        var lineIds = await ResolveLineIdsForSummaryAsync(filter, keys, ct);
        if (lineIds.Count == 0)
            return new List<AggregateSummaryPdfLineModel>();

        var baseLines = await _preparationWorkService.BuildPdfLineModelsAsync(lineIds, ct);
        return baseLines.Select(l => new AggregateSummaryPdfLineModel
        {
            WarehouseName = l.WarehouseName,
            ShipDateDisplay = l.DateDisplay,
            ReportItemName = l.ParentItemname,
            ChildItemCode = l.ChildItemcode,
            ChildItemName = l.ChildItemname,
            Quantity = l.Quantity,
            Unit = l.Unit
        }).ToList();
    }

    private static DateOnly? ParseYyyymmdd(string? s)
    {
        if (string.IsNullOrEmpty(s) || s.Length != 8) return null;
        if (int.TryParse(s.AsSpan(0, 4), out var y) &&
            int.TryParse(s.AsSpan(4, 2), out var m) &&
            int.TryParse(s.AsSpan(6, 2), out var d))
            return new DateOnly(y, m, d);
        return null;
    }

    private static string FormatDateDisplay(string yyyymmdd)
    {
        if (string.IsNullOrEmpty(yyyymmdd) || yyyymmdd.Length != 8) return yyyymmdd;
        return $"{yyyymmdd[..4]}-{yyyymmdd.Substring(4, 2)}-{yyyymmdd.Substring(6, 2)}";
    }
}

internal sealed class AggregateSummarySqlRow
{
    public string ShipDate { get; set; } = "";
    public string MajorCode { get; set; } = "";
    public string MajorName { get; set; } = "";
    public string MiddleCode { get; set; } = "";
    public string MiddleName { get; set; } = "";
    public int ChildItemCount { get; set; }
}

internal sealed class AggregateSummaryLineIdRow
{
    public long SalesOrderLineId { get; set; }
}

