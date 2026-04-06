using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using BaggingInstructions.Api.Core;
using BaggingInstructions.Api.DTOs;

namespace BaggingInstructions.Api.Services;

/// <summary>
/// 調味液配合表仕様用の検索・PDF 行生成サービス。
/// 主データは ordertable（ordertype=MO、ordertableid・itemcode・qty・needdate・releasedate）。
/// item / bom / workcenter。便のみ salesorderline.slotcode 経由で deliveryslot（ordertable に slot が無い前提）。
/// </summary>
public sealed class ProductionInstructionService
{
    private readonly AppDbContext _db;

    public ProductionInstructionService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// 作業区マスタ（複数選択プルダウン用）。調理指示書と同じ workcenter テーブル。
    /// </summary>
    public async Task<List<ProductionInstructionWorkcenterOptionDto>> ListWorkcentersAsync(CancellationToken ct = default)
    {
        return await _db.Workcenters.AsNoTracking()
            .OrderBy(w => w.SortOrder ?? int.MaxValue)
            .ThenBy(w => w.WorkcenterName ?? "")
            .Select(w => new ProductionInstructionWorkcenterOptionDto
            {
                Id = w.WorkcenterId,
                Name = w.WorkcenterName ?? ""
            })
            .ToListAsync(ct);
    }

    /// <summary>
    /// 便マスタ（複数選択プルダウン用）。
    /// </summary>
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

    /// <summary>
    /// 納期・作業区・便でオーダー行を検索する。1 行 = 1 ordertable レコード。
    /// ordertable.ordertype が MO（大文字・前後空白無視）の行のみ。
    /// 納期キーは COALESCE(needdate, releasedate)。作業区・便は未選択なら絞り込まない。
    /// </summary>
    public async Task<List<ProductionInstructionSearchRowDto>> SearchAsync(
        string needDate,
        IReadOnlyList<long>? workcenterIds,
        IReadOnlyList<string>? slotCodes,
        CancellationToken ct = default)
    {
        var date = ParseYyyymmdd(needDate);
        if (!date.HasValue)
            throw new ArgumentException("納期はYYYYMMDD形式（8桁）で指定してください。", nameof(needDate));

        var wcIds = (workcenterIds ?? Array.Empty<long>()).Where(id => id > 0).Distinct().ToArray();
        var slots = (slotCodes ?? Array.Empty<string>())
            .Select(s => (s ?? "").Trim())
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        // TO_CHAR は text を返すため、比較側も YYYYMMDD 文字列にする（DateOnly を渡すと text = date で失敗する）
        var needDateYyyymmdd = date.Value.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

        var rows = await _db.Database
            .SqlQuery<ProductionInstructionSearchSqlRow>($@"
SELECT
  ot.ordertableid AS ""OrderTableId"",
  COALESCE(ot.itemcode, i.itemcode, '') AS ""ItemCode"",
  COALESCE(i.itemname, '') AS ""ItemName"",
  TO_CHAR(
    COALESCE(ot.needdate, ot.releasedate),
    'YYYYMMDD'
  ) AS ""NeedDate"",
  COALESCE(ds.slotname, ds.slotcode, '') AS ""SlotDisplay"",
  COALESCE((
    SELECT string_agg(DISTINCT wc.workcentername, '、' ORDER BY wc.workcentername)
    FROM itemworkcentermapping m2
    INNER JOIN workcenter wc ON wc.workcentercode = m2.workcentercode
    WHERE m2.itemcode = COALESCE(ot.itemcode, i.itemcode)
  ), '') AS ""WorkplaceNames""
FROM ordertable ot
INNER JOIN item i ON i.itemcode = ot.itemcode
LEFT JOIN salesorderline sol ON sol.salesorderlineid = ot.salesorderlineid
LEFT JOIN deliveryslot ds ON ds.slotcode = sol.slotcode
WHERE UPPER(TRIM(COALESCE(ot.ordertype, ''))) = 'MO'
  AND TO_CHAR(
        COALESCE(ot.needdate, ot.releasedate),
        'YYYYMMDD'
      ) = {needDateYyyymmdd}
  AND ({slots.Length} = 0 OR COALESCE(ds.slotcode, '') = ANY ({slots}))
  AND ({wcIds.Length} = 0 OR EXISTS (
        SELECT 1 FROM itemworkcentermapping m3
        INNER JOIN workcenter wc ON wc.workcentercode = m3.workcentercode
        WHERE m3.itemcode = COALESCE(ot.itemcode, i.itemcode) AND wc.workcenterid = ANY ({wcIds})
      ))
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
            var asof = h.NeedDate ?? h.ReleaseDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
            if (!bomCache.TryGetValue(h.ParentItemcode, out var boms))
            {
                boms = await FetchBomsForParentAsync(h.ParentItemcode, asof, ct);
                bomCache[h.ParentItemcode] = boms;
            }

            var qtyU0 = CookingInstructionQuantity.ResolveParentQtyInUnit0(
                h.RawOrdertableQty,
                h.QtyUni0,
                h.QtyUni1,
                h.QtyUni2,
                h.QtyUni3,
                h.IaStd,
                h.IaCar0,
                h.ConversionValue1,
                h.ConversionValue2,
                h.ConversionValue3);
            var (dispQty, dispUnit) = CookingInstructionQuantity.ParentPlannedQtyDisplay(
                qtyU0,
                h.QtyUni1,
                h.ProcurementUnitName,
                h.ParentUnitName,
                h.ConversionValue1);
            var parentQtyDisplay = dispQty.ToString("0.###", CultureInfo.InvariantCulture);
            var parentUnitName = dispUnit;

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
                    ChildYieldPercentDisplay = "",
                    NeedDateDisplay = h.NeedDate?.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture) ?? "",
                    SlotDisplay = h.SlotDisplay
                });
                continue;
            }

            foreach (var b in boms)
            {
                var qty = PreparationBomQuantity.ComputeRequiredQty(qtyU0, b.InputQty, b.OutputQty, b.YieldPercent);
                var qtyDisplay = qty.ToString("0.###", CultureInfo.InvariantCulture);
                var yieldDisplay = b.YieldPercent.ToString("0.###", CultureInfo.InvariantCulture);

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
                    ChildYieldPercentDisplay = yieldDisplay,
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
                  COALESCE(ot.qty, 0) AS raw_mfg_qty,
                  ot.qtyuni0 AS ot_qtyuni0,
                  ot.qtyuni1 AS ot_qtyuni1,
                  ot.qtyuni2 AS ot_qtyuni2,
                  ot.qtyuni3 AS ot_qtyuni3,
                  COALESCE(ot.itemcode, i.itemcode, '') AS parent_itemcode,
                  COALESCE(i.itemname, '') AS parent_itemname,
                  COALESCE(u0.unitname, '') AS parent_unitname,
                  u1.unitname AS procurement_u_name,
                  ia.std AS ia_std,
                  ia.car0 AS ia_car0,
                  i.conversionvalue1 AS cv1,
                  i.conversionvalue2 AS cv2,
                  i.conversionvalue3 AS cv3,
                  COALESCE(ds.slotname, ds.slotcode, '') AS slot_display,
                  COALESCE(ot.needdate, ot.releasedate) AS need_date,
                  ot.releasedate,
                  ot.ordertableid::text AS order_no
                FROM ordertable ot
                INNER JOIN item i ON i.itemcode = ot.itemcode
                LEFT JOIN itemadditionalinformation ia ON ia.itemcode = i.itemcode
                LEFT JOIN salesorderline sol ON sol.salesorderlineid = ot.salesorderlineid
                LEFT JOIN unit u0 ON u0.unitcode = i.unitcode0
                LEFT JOIN unit u1 ON u1.unitcode = i.unitcode1
                LEFT JOIN deliveryslot ds ON ds.slotcode = sol.slotcode
                WHERE ot.ordertableid = ANY(@ids)
                  AND UPPER(TRIM(COALESCE(ot.ordertype, ''))) = 'MO'
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
                    RawOrdertableQty = reader.GetDecimal(1),
                    QtyUni0 = ReadDecimalNullable(reader, 2),
                    QtyUni1 = ReadDecimalNullable(reader, 3),
                    QtyUni2 = ReadDecimalNullable(reader, 4),
                    QtyUni3 = ReadDecimalNullable(reader, 5),
                    ParentItemcode = reader.GetString(6),
                    ParentItemname = reader.GetString(7),
                    ParentUnitName = reader.GetString(8),
                    ProcurementUnitName = reader.IsDBNull(9) ? null : reader.GetString(9),
                    IaStd = reader.IsDBNull(10) ? null : reader.GetString(10),
                    IaCar0 = ReadDecimalNullable(reader, 11),
                    ConversionValue1 = ReadDecimalNullable(reader, 12),
                    ConversionValue2 = ReadDecimalNullable(reader, 13),
                    ConversionValue3 = ReadDecimalNullable(reader, 14),
                    SlotDisplay = reader.GetString(15),
                    NeedDate = ReadDateNullable(reader, 16),
                    ReleaseDate = ReadDateNullable(reader, 17),
                    OrderNo = reader.GetString(18)
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

    private static decimal? ReadDecimalNullable(NpgsqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetDecimal(ordinal);

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

internal sealed class ProductionInstructionSlotSqlRow
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
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
    public decimal RawOrdertableQty { get; set; }
    public decimal? QtyUni0 { get; set; }
    public decimal? QtyUni1 { get; set; }
    public decimal? QtyUni2 { get; set; }
    public decimal? QtyUni3 { get; set; }
    public string ParentItemcode { get; set; } = "";
    public string ParentItemname { get; set; } = "";
    public string ParentUnitName { get; set; } = "";
    public string? ProcurementUnitName { get; set; }
    public string? IaStd { get; set; }
    public decimal? IaCar0 { get; set; }
    public decimal? ConversionValue1 { get; set; }
    public decimal? ConversionValue2 { get; set; }
    public decimal? ConversionValue3 { get; set; }
    public string SlotDisplay { get; set; } = "";
    public DateOnly? NeedDate { get; set; }
    public DateOnly? ReleaseDate { get; set; }
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

