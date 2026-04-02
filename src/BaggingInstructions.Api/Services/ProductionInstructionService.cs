using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using BaggingInstructions.Api.Core;
using BaggingInstructions.Api.DTOs;

namespace BaggingInstructions.Api.Services;

/// <summary>
/// 製造指示書用の検索・PDF 行生成サービス。
/// ordertable / salesorderline / item / bom / deliveryslot / workcenter を用いて、
/// 行ヘッダと BOM 展開を行う。
/// </summary>
public sealed class ProductionInstructionService
{
    private readonly AppDbContext _db;

    public ProductionInstructionService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// 納期・作業区・便でオーダー行を検索する。1 行 = 1 ordertable レコード。
    /// </summary>
    public async Task<List<ProductionInstructionSearchRowDto>> SearchAsync(
        string needDate,
        string? workcenterFilter,
        string? slotFilter,
        CancellationToken ct = default)
    {
        var date = ParseYyyymmdd(needDate);
        if (!date.HasValue)
            throw new ArgumentException("納期はYYYYMMDD形式（8桁）で指定してください。", nameof(needDate));

        var workF = (workcenterFilter ?? "").Trim();
        var slotF = (slotFilter ?? "").Trim();

        var rows = await _db.Database
            .SqlQuery<ProductionInstructionSearchSqlRow>($@"
SELECT
  ot.ordertableid AS ""OrderTableId"",
  i.itemcode AS ""ItemCode"",
  COALESCE(i.itemname, '') AS ""ItemName"",
  TO_CHAR(
    COALESCE(
      ot.needdate,
      sol.planneddeliverydate
    ),
    'YYYYMMDD'
  ) AS ""NeedDate"",
  COALESCE(ds.slotname, ds.slotcode, '') AS ""SlotDisplay"",
  COALESCE((
    {SqlFragments.WorkplaceNamesByItemcode("i.itemcode")}
  ), '') AS ""WorkplaceNames""
FROM ordertable ot
INNER JOIN salesorderline sol ON sol.salesorderlineid = ot.salesorderlineid
INNER JOIN item i ON i.itemcode = sol.itemcode
LEFT JOIN deliveryslot ds ON ds.slotcode = sol.slotcode
WHERE TO_CHAR(
        COALESCE(
          ot.needdate,
          sol.planneddeliverydate
        ),
        'YYYYMMDD'
      ) = {date.Value}
  AND ({slotF} = '' OR COALESCE(ds.slotcode, '') ILIKE '%' || {slotF} || '%' OR COALESCE(ds.slotname, '') ILIKE '%' || {slotF} || '%')
  AND ({workF} = '' OR COALESCE((
        {SqlFragments.WorkplaceNamesByItemcode("i.itemcode")}
      ), '') ILIKE '%' || {workF} || '%')
ORDER BY i.itemname, ds.slotcode, ot.ordertableid
")
            .ToListAsync(ct);

        return rows.Select(r => new ProductionInstructionSearchRowDto
        {
            OrderTableId = r.OrderTableId,
            ItemCode = r.ItemCode ?? "",
            ItemName = r.ItemName ?? "",
            NeedDate = FormatDateDisplay(r.NeedDate),
            SlotDisplay = r.SlotDisplay ?? ""
        }).ToList();
    }

    /// <summary>
    /// PDF 用の明細行を生成する。orderIds は ordertableid。
    /// </summary>
    public async Task<List<ProductionInstructionPdfLineModel>> BuildPdfLineModelsAsync(
        IReadOnlyList<long> orderIds,
        CancellationToken ct = default)
    {
        if (orderIds == null || orderIds.Count == 0)
            return new List<ProductionInstructionPdfLineModel>();

        var headers = await FetchLineHeadersAsync(orderIds, ct);
        if (headers.Count == 0)
            return new List<ProductionInstructionPdfLineModel>();

        var bomCache = new Dictionary<string, List<ProductionInstructionBomSqlRow>>(StringComparer.Ordinal);

        var lines = new List<ProductionInstructionPdfLineModel>();
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
                lines.Add(new ProductionInstructionPdfLineModel
                {
                    OrderNo = h.OrderNo ?? "",
                    ParentItemCode = h.ParentItemcode,
                    ParentItemName = h.ParentItemname,
                    PlannedQuantityDisplay = parentQtyDisplay,
                    PlanUnitName = parentUnitName,
                    ChildItemCode = "",
                    ChildItemName = "",
                    ChildSpec = "",
                    ChildRequiredQtyDisplay = "",
                    ChildUnitName = "",
                    NeedDateDisplay = h.NeedDate?.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture) ?? "",
                    SlotDisplay = h.SlotDisplay
                });
                continue;
            }

            foreach (var b in boms)
            {
                var qty = PreparationBomQuantity.ComputeRequiredQty(h.MfgQty, b.InputQty, b.OutputQty, b.YieldPercent);
                var qtyDisplay = qty.ToString("0.###", CultureInfo.InvariantCulture);

                lines.Add(new ProductionInstructionPdfLineModel
                {
                    OrderNo = h.OrderNo ?? "",
                    ParentItemCode = h.ParentItemcode,
                    ParentItemName = h.ParentItemname,
                    PlannedQuantityDisplay = parentQtyDisplay,
                    PlanUnitName = parentUnitName,
                    ChildItemCode = b.ChildItemcode,
                    ChildItemName = b.ChildItemname ?? "",
                    ChildSpec = b.ChildSpec ?? "",
                    ChildRequiredQtyDisplay = qtyDisplay,
                    ChildUnitName = b.ChildUnitname ?? "",
                    NeedDateDisplay = h.NeedDate?.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture) ?? "",
                    SlotDisplay = h.SlotDisplay
                });
            }
        }

        return lines;
    }

    private async Task<List<ProductionInstructionLineHeaderRow>> FetchLineHeadersAsync(
        IReadOnlyList<long> orderIds,
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
                  ot.ordertableid,
                  sol.salesorderid,
                  COALESCE(
                    ot.qty,
                    sol.quantity
                  ) AS mfg_qty,
                  i.itemcode AS parent_itemcode,
                  COALESCE(i.itemname, '') AS parent_itemname,
                  COALESCE(u0.unitname, '') AS parent_unitname,
                  COALESCE(ds.slotname, ds.slotcode, '') AS slot_display,
                  COALESCE(
                    ot.needdate,
                    sol.planneddeliverydate
                  ) AS need_date,
                  sol.planneddeliverydate,
                  COALESCE(sol.salesorderid::text, '') AS order_no
                FROM ordertable ot
                INNER JOIN salesorderline sol ON sol.salesorderlineid = ot.salesorderlineid
                INNER JOIN item i ON i.itemcode = sol.itemcode
                LEFT JOIN unit u0 ON u0.unitcode = i.unitcode0
                LEFT JOIN deliveryslot ds ON ds.slotcode = sol.slotcode
                WHERE ot.ordertableid = ANY(@ids)
                ORDER BY ot.ordertableid
                """, conn);
            cmd.Parameters.Add(new NpgsqlParameter("ids", NpgsqlDbType.Bigint | NpgsqlDbType.Array)
            {
                Value = orderIds.ToArray()
            });

            var list = new List<ProductionInstructionLineHeaderRow>();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                list.Add(new ProductionInstructionLineHeaderRow
                {
                    OrderTableId = reader.GetInt64(0),
                    Salesorderid = reader.GetInt64(1),
                    MfgQty = reader.GetDecimal(2),
                    ParentItemcode = reader.GetString(3),
                    ParentItemname = reader.GetString(4),
                    ParentUnitName = reader.GetString(5),
                    SlotDisplay = reader.GetString(6),
                    NeedDate = ReadDateNullable(reader, 7),
                    PlannedDeliveryDate = ReadDateNullable(reader, 8),
                    OrderNo = reader.GetString(9)
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

    private async Task<List<ProductionInstructionBomSqlRow>> FetchBomsForParentAsync(
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
                  COALESCE(u.unitname, '') AS child_unitname,
                  COALESCE(ia.std, '') AS child_spec
                FROM bom b
                LEFT JOIN item ci ON ci.itemcode = b.childitemcode
                LEFT JOIN unit u ON u.unitcode = ci.unitcode0
                LEFT JOIN itemadditionalinformation ia ON ia.itemcode = b.childitemcode
                WHERE b.parentitemcode = @p
                  AND b.childitemcode IS NOT NULL
                  AND (b.startdate IS NULL OR b.startdate <= @asof)
                  AND (b.enddate IS NULL OR b.enddate >= @asof)
                ORDER BY b.productionorder NULLS LAST, b.childitemcode
                """, conn);
            cmd.Parameters.AddWithValue("p", parentItemcode);
            cmd.Parameters.AddWithValue("asof", asOf);

            var list = new List<ProductionInstructionBomSqlRow>();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                list.Add(new ProductionInstructionBomSqlRow
                {
                    ChildItemcode = reader.GetString(0),
                    InputQty = reader.GetDecimal(1),
                    OutputQty = reader.GetDecimal(2),
                    YieldPercent = reader.GetDecimal(3),
                    ChildItemname = reader.GetString(4),
                    ChildUnitname = reader.GetString(5),
                    ChildSpec = reader.GetString(6)
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

internal sealed class ProductionInstructionSearchSqlRow
{
    public long OrderTableId { get; set; }
    public string ItemCode { get; set; } = "";
    public string ItemName { get; set; } = "";
    public string NeedDate { get; set; } = "";
    public string SlotDisplay { get; set; } = "";
    public string WorkplaceNames { get; set; } = "";
}

internal sealed class ProductionInstructionLineHeaderRow
{
    public long OrderTableId { get; set; }
    public long Salesorderid { get; set; }
    public decimal MfgQty { get; set; }
    public string ParentItemcode { get; set; } = "";
    public string ParentItemname { get; set; } = "";
    public string ParentUnitName { get; set; } = "";
    public string SlotDisplay { get; set; } = "";
    public DateOnly? NeedDate { get; set; }
    public DateOnly? PlannedDeliveryDate { get; set; }
    public string OrderNo { get; set; } = "";
}

internal sealed class ProductionInstructionBomSqlRow
{
    public string ChildItemcode { get; set; } = "";
    public decimal InputQty { get; set; }
    public decimal OutputQty { get; set; }
    public decimal YieldPercent { get; set; }
    public string? ChildItemname { get; set; }
    public string? ChildUnitname { get; set; }
    public string? ChildSpec { get; set; }
}

