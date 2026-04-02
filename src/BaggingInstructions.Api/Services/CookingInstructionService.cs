using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using BaggingInstructions.Api.Core;
using BaggingInstructions.Api.DTOs;

namespace BaggingInstructions.Api.Services;

/// <summary>
/// 調理指示書用の検索・PDF 行生成サービス。
/// salesorderline / ordertable / item / bom / workcenter / deliveryslot を用いて、
/// 行ヘッダと BOM 展開を行う。
/// </summary>
public sealed class CookingInstructionService
{
    private readonly AppDbContext _db;

    public CookingInstructionService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// 納期・作業区・便で親行を検索する。1 行 = 1 salesorderline。
    /// </summary>
    public async Task<List<CookingInstructionSearchRowDto>> SearchAsync(
        string needDate,
        string? workplaceFilter,
        string? slotFilter,
        CancellationToken ct = default)
    {
        var date = ParseYyyymmdd(needDate);
        if (!date.HasValue)
            throw new ArgumentException("納期はYYYYMMDD形式（8桁）で指定してください。", nameof(needDate));

        var workF = (workplaceFilter ?? "").Trim();
        var slotF = (slotFilter ?? "").Trim();

        var rows = await _db.Database
            .SqlQuery<CookingInstructionSearchSqlRow>($@"
SELECT
  sol.salesorderlineid AS ""SalesOrderLineId"",
  TO_CHAR(
    COALESCE(
      (SELECT ot.needdate FROM ordertable ot WHERE ot.salesorderlineid = sol.salesorderlineid ORDER BY ot.ordertableid LIMIT 1),
      sol.planneddeliverydate
    ),
    'YYYYMMDD'
  ) AS ""NeedDate"",
  COALESCE(ds.slotname, ds.slotcode, '') AS ""SlotDisplay"",
  COALESCE((
    SELECT string_agg(DISTINCT wc.workcentername, '、' ORDER BY wc.workcentername)
    FROM itemworkcentermapping m2
    INNER JOIN workcenter wc ON wc.workcentercode = m2.workcentercode
    WHERE m2.itemcode = i.itemcode
  ), '') AS ""WorkplaceNames"",
  i.itemcode AS ""ParentItemCode"",
  COALESCE(i.itemname, '') AS ""ParentItemName""
FROM salesorderline sol
INNER JOIN item i ON i.itemcode = sol.itemcode
LEFT JOIN deliveryslot ds ON ds.slotcode = sol.slotcode
WHERE COALESCE(
        (SELECT ot.needdate FROM ordertable ot WHERE ot.salesorderlineid = sol.salesorderlineid ORDER BY ot.ordertableid LIMIT 1),
        sol.planneddeliverydate
      ) = {date.Value}
  AND ({slotF} = '' OR COALESCE(ds.slotcode, '') ILIKE '%' || {slotF} || '%' OR COALESCE(ds.slotname, '') ILIKE '%' || {slotF} || '%')
  AND ({workF} = '' OR COALESCE((
        SELECT string_agg(DISTINCT wc2.workcentername, '、' ORDER BY wc2.workcentername)
        FROM itemworkcentermapping m3
        INNER JOIN workcenter wc2 ON wc2.workcentercode = m3.workcentercode
        WHERE m3.itemcode = i.itemcode
      ), '') ILIKE '%' || {workF} || '%')
ORDER BY sol.salesorderlineid
")
            .ToListAsync(ct);

        return rows.Select(r => new CookingInstructionSearchRowDto
        {
            SalesOrderLineId = r.SalesOrderLineId,
            NeedDate = FormatDateDisplay(r.NeedDate),
            SlotDisplay = r.SlotDisplay ?? "",
            WorkplaceNames = r.WorkplaceNames ?? "",
            ParentItemCode = r.ParentItemCode ?? "",
            ParentItemName = r.ParentItemName ?? ""
        }).ToList();
    }

    /// <summary>
    /// PDF 用の明細行を生成する。lineIds は salesorderlineid。
    /// </summary>
    public async Task<List<CookingInstructionPdfLineModel>> BuildPdfLineModelsAsync(
        IReadOnlyList<long> lineIds,
        CancellationToken ct = default)
    {
        if (lineIds == null || lineIds.Count == 0)
            return new List<CookingInstructionPdfLineModel>();

        var headers = await FetchLineHeadersAsync(lineIds, ct);
        if (headers.Count == 0)
            return new List<CookingInstructionPdfLineModel>();

        var bomCache = new Dictionary<string, List<CookingInstructionBomSqlRow>>(StringComparer.Ordinal);

        var lines = new List<CookingInstructionPdfLineModel>();
        foreach (var h in headers)
        {
            var asof = h.NeedDate ?? h.PlannedDeliveryDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
            if (!bomCache.TryGetValue(h.ParentItemcode, out var boms))
            {
                boms = await FetchBomsForParentAsync(h.ParentItemcode, asof, ct);
                bomCache[h.ParentItemcode] = boms;
            }

            var parentQtyDisplay = h.MfgQty.ToString("0.###", CultureInfo.InvariantCulture);
            var parentUnitName = h.ParentUnitName ?? "";

            if (boms.Count == 0)
            {
                lines.Add(new CookingInstructionPdfLineModel
                {
                    OrderNo = h.Salesorderid.ToString(CultureInfo.InvariantCulture),
                    ParentItemCode = h.ParentItemcode,
                    ParentItemName = h.ParentItemname,
                    PlannedQuantityDisplay = parentQtyDisplay,
                    PlanUnitName = parentUnitName,
                    ChildItemCode = "",
                    ChildItemName = "",
                    ChildRequiredQtyDisplay = "",
                    ChildUnitName = "",
                    NeedDateDisplay = h.NeedDate?.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture) ?? "",
                    SlotDisplay = h.SlotDisplay,
                    WorkplaceNames = h.WorkplaceNames
                });
                continue;
            }

            foreach (var b in boms)
            {
                var qty = PreparationBomQuantity.ComputeRequiredQty(h.MfgQty, b.InputQty, b.OutputQty, b.YieldPercent);
                var qtyDisplay = qty.ToString("0.###", CultureInfo.InvariantCulture);

                lines.Add(new CookingInstructionPdfLineModel
                {
                    OrderNo = h.Salesorderid.ToString(CultureInfo.InvariantCulture),
                    ParentItemCode = h.ParentItemcode,
                    ParentItemName = h.ParentItemname,
                    PlannedQuantityDisplay = parentQtyDisplay,
                    PlanUnitName = parentUnitName,
                    ChildItemCode = b.ChildItemcode,
                    ChildItemName = b.ChildItemname ?? "",
                    ChildRequiredQtyDisplay = qtyDisplay,
                    ChildUnitName = b.ChildUnitname ?? "",
                    NeedDateDisplay = h.NeedDate?.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture) ?? "",
                    SlotDisplay = h.SlotDisplay,
                    WorkplaceNames = h.WorkplaceNames
                });
            }
        }

        return lines;
    }

    private async Task<List<CookingInstructionLineHeaderRow>> FetchLineHeadersAsync(
        IReadOnlyList<long> lineIds,
        CancellationToken ct)
    {
        var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
        var shouldClose = conn.State != ConnectionState.Open;
        if (shouldClose)
            await conn.OpenAsync(ct);
        try
        {
            await using var cmd = new NpgsqlCommand(
                """
                SELECT
                  sol.salesorderlineid,
                  sol.salesorderid,
                  COALESCE(
                    (SELECT ot.qty FROM ordertable ot WHERE ot.salesorderlineid = sol.salesorderlineid ORDER BY ot.ordertableid LIMIT 1),
                    (SELECT ot.qty FROM ordertable ot WHERE ot.itemcode = sol.itemcode
                     ORDER BY abs(ot.needdate - sol.planneddeliverydate) NULLS LAST, ot.ordertableid DESC LIMIT 1),
                    sol.quantity
                  ) AS mfg_qty,
                  i.itemcode AS parent_itemcode,
                  COALESCE(i.itemname, '') AS parent_itemname,
                  COALESCE(ds.slotname, ds.slotcode, '') AS slot_display,
                  COALESCE((
                    SELECT string_agg(DISTINCT wc.workcentername, '、' ORDER BY wc.workcentername)
                    FROM itemworkcentermapping m2
                    INNER JOIN workcenter wc ON wc.workcentercode = m2.workcentercode
                    WHERE m2.itemcode = i.itemcode
                  ), '') AS workplace_names,
                  sol.planneddeliverydate,
                  COALESCE(
                    (SELECT ot.needdate FROM ordertable ot WHERE ot.salesorderlineid = sol.salesorderlineid ORDER BY ot.ordertableid LIMIT 1),
                    (SELECT ot.needdate FROM ordertable ot WHERE ot.itemcode = sol.itemcode AND ot.needdate IS NOT NULL
                     ORDER BY abs(ot.needdate - sol.planneddeliverydate) NULLS LAST, ot.ordertableid DESC LIMIT 1),
                    sol.planneddeliverydate
                  ) AS need_date,
                  COALESCE(u0.unitname, '') AS parent_unitname
                FROM salesorderline sol
                INNER JOIN item i ON i.itemcode = sol.itemcode
                LEFT JOIN unit u0 ON u0.unitcode = i.unitcode0
                LEFT JOIN deliveryslot ds ON ds.slotcode = sol.slotcode
                WHERE sol.salesorderlineid = ANY(@ids)
                ORDER BY sol.salesorderlineid
                """, conn);
            cmd.Parameters.Add(new NpgsqlParameter("ids", NpgsqlDbType.Bigint | NpgsqlDbType.Array)
            {
                Value = lineIds.ToArray()
            });

            var list = new List<CookingInstructionLineHeaderRow>();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                list.Add(new CookingInstructionLineHeaderRow
                {
                    Salesorderlineid = reader.GetInt64(0),
                    Salesorderid = reader.GetInt64(1),
                    MfgQty = reader.GetDecimal(2),
                    ParentItemcode = reader.GetString(3),
                    ParentItemname = reader.GetString(4),
                    SlotDisplay = reader.GetString(5),
                    WorkplaceNames = reader.GetString(6),
                    PlannedDeliveryDate = ReadDateNullable(reader, 7),
                    NeedDate = ReadDateNullable(reader, 8),
                    ParentUnitName = reader.GetString(9)
                });
            }

            return list;
        }
        finally
        {
            if (shouldClose && conn.State == ConnectionState.Open)
                await conn.CloseAsync();
        }
    }

    private async Task<List<CookingInstructionBomSqlRow>> FetchBomsForParentAsync(
        string parentItemcode,
        DateOnly asOf,
        CancellationToken ct)
    {
        var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
        var shouldClose = conn.State != ConnectionState.Open;
        if (shouldClose)
            await conn.OpenAsync(ct);
        try
        {
            await using var cmd = new NpgsqlCommand(
                """
                SELECT
                  b.childitemcode,
                  b.inputqty,
                  b.outputqty,
                  b.yieldpercent,
                  COALESCE(ci.itemname, '') AS child_itemname,
                  COALESCE(u.unitname, '') AS child_unitname
                FROM bom b
                LEFT JOIN item ci ON ci.itemcode = b.childitemcode
                LEFT JOIN unit u ON u.unitcode = ci.unitcode0
                WHERE b.parentitemcode = @p
                  AND b.childitemcode IS NOT NULL
                  AND (b.startdate IS NULL OR b.startdate <= @asof)
                  AND (b.enddate IS NULL OR b.enddate >= @asof)
                ORDER BY b.productionorder NULLS LAST, b.childitemcode
                """, conn);
            cmd.Parameters.AddWithValue("p", parentItemcode);
            cmd.Parameters.AddWithValue("asof", asOf);

            var list = new List<CookingInstructionBomSqlRow>();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                list.Add(new CookingInstructionBomSqlRow
                {
                    ChildItemcode = reader.GetString(0),
                    InputQty = reader.GetDecimal(1),
                    OutputQty = reader.GetDecimal(2),
                    YieldPercent = reader.GetDecimal(3),
                    ChildItemname = reader.GetString(4),
                    ChildUnitname = reader.GetString(5)
                });
            }

            return list;
        }
        finally
        {
            if (shouldClose && conn.State == ConnectionState.Open)
                await conn.CloseAsync();
        }
    }

    private static DateOnly? ReadDateNullable(NpgsqlDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
            return null;
        var o = reader.GetValue(ordinal);
        if (o is DateOnly d)
            return d;
        if (o is DateTime dt)
            return DateOnly.FromDateTime(dt);
        return null;
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

internal sealed class CookingInstructionSearchSqlRow
{
    public long SalesOrderLineId { get; set; }
    public string NeedDate { get; set; } = "";
    public string SlotDisplay { get; set; } = "";
    public string WorkplaceNames { get; set; } = "";
    public string ParentItemCode { get; set; } = "";
    public string ParentItemName { get; set; } = "";
}

internal sealed class CookingInstructionLineHeaderRow
{
    public long Salesorderlineid { get; set; }
    public long Salesorderid { get; set; }
    public decimal MfgQty { get; set; }
    public string ParentItemcode { get; set; } = "";
    public string ParentItemname { get; set; } = "";
    public string SlotDisplay { get; set; } = "";
    public string WorkplaceNames { get; set; } = "";
    public DateOnly? PlannedDeliveryDate { get; set; }
    public DateOnly? NeedDate { get; set; }
    public string ParentUnitName { get; set; } = "";
}

internal sealed class CookingInstructionBomSqlRow
{
    public string ChildItemcode { get; set; } = "";
    public decimal InputQty { get; set; }
    public decimal OutputQty { get; set; }
    public decimal YieldPercent { get; set; }
    public string? ChildItemname { get; set; }
    public string? ChildUnitname { get; set; }
}

