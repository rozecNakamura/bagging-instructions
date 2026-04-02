using System.Data;
using System.Globalization;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using BaggingInstructions.Api.Core;
using BaggingInstructions.Api.DTOs;

namespace BaggingInstructions.Api.Services;

/// <summary>
/// 調理指示書: 検索は ordertable 起点（MO のみ）。PDF は ordertableid 単位。
/// </summary>
public sealed class CookingInstructionService
{
    public const string CookingReportCode = "COOKING_INSTRUCTION";

    private readonly AppDbContext _db;

    public CookingInstructionService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<CookingInstructionWorkcenterOptionDto>> ListWorkcentersAsync(CancellationToken ct = default)
    {
        return await _db.Workcenters.AsNoTracking()
            .OrderBy(w => w.SortOrder ?? int.MaxValue)
            .ThenBy(w => w.WorkcenterName ?? "")
            .Select(w => new CookingInstructionWorkcenterOptionDto
            {
                Id = w.WorkcenterId,
                Name = w.WorkcenterName ?? ""
            })
            .ToListAsync(ct);
    }

    public async Task<List<CookingInstructionSlotOptionDto>> ListSlotsAsync(CancellationToken ct = default)
    {
        var rows = await _db.Database
            .SqlQuery<CookingInstructionSlotSqlRow>($@"
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
            .Select(r => new CookingInstructionSlotOptionDto
            {
                Code = r.Code ?? "",
                Name = r.Name ?? ""
            })
            .ToList();
    }

    /// <summary>
    /// 1 行 = 1 ordertable（MO）。納期は COALESCE(needdate, releasedate)。作業区・便は未選択なら絞り込まない。
    /// </summary>
    public async Task<List<CookingInstructionSearchRowDto>> SearchAsync(
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

        var needDateYyyymmdd = date.Value.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

        var rows = await _db.Database
            .SqlQuery<CookingInstructionSearchSqlRow>($@"
SELECT
  ot.ordertableid AS ""OrderTableId"",
  COALESCE(i.itemname, '') AS ""ItemName"",
  TO_CHAR(
    COALESCE(ot.needdate, ot.releasedate),
    'YYYYMMDD'
  ) AS ""NeedDate"",
  COALESCE(ds.slotname, ds.slotcode, '') AS ""SlotDisplay""
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

        return rows.Select(r => new CookingInstructionSearchRowDto
        {
            OrderTableId = r.OrderTableId,
            ItemName = r.ItemName ?? "",
            NeedDate = FormatDateDisplay(r.NeedDate),
            SlotDisplay = r.SlotDisplay ?? ""
        }).ToList();
    }

    /// <summary>
    /// PDF 行生成。ids は ordertableid。
    /// </summary>
    public async Task<List<CookingInstructionPdfLineModel>> BuildPdfLineModelsAsync(
        IReadOnlyList<long> orderTableIds,
        CancellationToken ct = default)
    {
        if (orderTableIds == null || orderTableIds.Count == 0)
            return new List<CookingInstructionPdfLineModel>();

        var headers = await FetchLineHeadersAsync(orderTableIds, ct);
        if (headers.Count == 0)
            return new List<CookingInstructionPdfLineModel>();

        var bomCache = new Dictionary<string, List<CookingInstructionBomSqlRow>>(StringComparer.Ordinal);

        var lines = new List<CookingInstructionPdfLineModel>();
        foreach (var h in headers)
        {
            var asof = h.NeedDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
            if (!bomCache.TryGetValue(h.ParentItemcode, out var boms))
            {
                boms = await FetchBomsForParentAsync(h.ParentItemcode, asof, ct);
                bomCache[h.ParentItemcode] = boms;
            }

            var qtyU0 = CookingInstructionQuantity.ToParentQtyInUnit0(h.RawOrdertableQty, h.IaStd, h.IaCar0);
            var (dispQty, dispUnit) = CookingInstructionQuantity.ParentDisplayForPdf(
                qtyU0, h.ProcurementUnitName, h.Unit0Name, h.IaCar1);
            var parentQtyDisplay = dispQty.ToString("0.###", CultureInfo.InvariantCulture);
            var parentUnitName = dispUnit;

            if (boms.Count == 0)
            {
                lines.Add(new CookingInstructionPdfLineModel
                {
                    OrderNo = h.OrderNo ?? "",
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
                var qty = PreparationBomQuantity.ComputeRequiredQty(qtyU0, b.InputQty, b.OutputQty, b.YieldPercent);
                var qtyDisplay = qty.ToString("0.###", CultureInfo.InvariantCulture);

                lines.Add(new CookingInstructionPdfLineModel
                {
                    OrderNo = h.OrderNo ?? "",
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

    /// <summary>
    /// reportoutputsetting → 無ければ rxz の Form/@Name → 既定「調理指示書」。
    /// </summary>
    public async Task<string> ResolveCookingInstructionReportTitleAsync(string rxzTemplatePath, CancellationToken ct = default)
    {
        try
        {
            var name = await _db.ReportOutputSettings.AsNoTracking()
                .Where(r => r.ReportCode == CookingReportCode)
                .Select(r => r.DisplayName)
                .FirstOrDefaultAsync(ct);
            if (!string.IsNullOrWhiteSpace(name))
                return name!.Trim();
        }
        catch
        {
            // Table or column may not exist in some deployments.
        }

        var fromRxz = ReadFormNameFromRxz(rxzTemplatePath);
        if (!string.IsNullOrWhiteSpace(fromRxz))
            return fromRxz.Trim();

        return "調理指示書";
    }

    private static string? ReadFormNameFromRxz(string rxzPath)
    {
        try
        {
            var doc = XDocument.Load(rxzPath);
            var form = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Form");
            return form?.Attribute("Name")?.Value;
        }
        catch
        {
            return null;
        }
    }

    private async Task<List<CookingInstructionLineHeaderRow>> FetchLineHeadersAsync(
        IReadOnlyList<long> orderTableIds,
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
                  COALESCE(ot.itemcode, i.itemcode, '') AS parent_itemcode,
                  COALESCE(i.itemname, '') AS parent_itemname,
                  COALESCE(u0.unitname, '') AS parent_u0_name,
                  u1.unitname AS procurement_u_name,
                  ia.std AS ia_std,
                  ia.car0 AS ia_car0,
                  ia.car1 AS ia_car1,
                  COALESCE(ds.slotname, ds.slotcode, '') AS slot_display,
                  COALESCE((
                    SELECT string_agg(DISTINCT wc.workcentername, '、' ORDER BY wc.workcentername)
                    FROM itemworkcentermapping m2
                    INNER JOIN workcenter wc ON wc.workcentercode = m2.workcentercode
                    WHERE m2.itemcode = COALESCE(ot.itemcode, i.itemcode)
                  ), '') AS workplace_names,
                  COALESCE(ot.needdate, ot.releasedate) AS need_date,
                  COALESCE(sol.salesorderid::text, ot.ordertableid::text) AS order_no_for_pdf
                FROM ordertable ot
                INNER JOIN item i ON i.itemcode = ot.itemcode
                LEFT JOIN itemadditionalinformation ia ON ia.itemcode = i.itemcode
                LEFT JOIN unit u0 ON u0.unitcode = i.unitcode0
                LEFT JOIN unit u1 ON u1.unitcode = i.unitcode1
                LEFT JOIN salesorderline sol ON sol.salesorderlineid = ot.salesorderlineid
                LEFT JOIN deliveryslot ds ON ds.slotcode = sol.slotcode
                WHERE ot.ordertableid = ANY(@ids)
                  AND UPPER(TRIM(COALESCE(ot.ordertype, ''))) = 'MO'
                ORDER BY ot.ordertableid
                """, conn);
            cmd.Parameters.Add(new NpgsqlParameter("ids", NpgsqlDbType.Bigint | NpgsqlDbType.Array)
            {
                Value = orderTableIds.ToArray()
            });

            var list = new List<CookingInstructionLineHeaderRow>();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                list.Add(new CookingInstructionLineHeaderRow
                {
                    OrderTableId = reader.GetInt64(0),
                    RawOrdertableQty = reader.GetDecimal(1),
                    ParentItemcode = reader.GetString(2),
                    ParentItemname = reader.GetString(3),
                    Unit0Name = reader.GetString(4),
                    ProcurementUnitName = reader.IsDBNull(5) ? null : reader.GetString(5),
                    IaStd = reader.IsDBNull(6) ? null : reader.GetString(6),
                    IaCar0 = reader.IsDBNull(7) ? null : reader.GetDecimal(7),
                    IaCar1 = reader.IsDBNull(8) ? null : reader.GetDecimal(8),
                    SlotDisplay = reader.GetString(9),
                    WorkplaceNames = reader.GetString(10),
                    NeedDate = ReadDateNullable(reader, 11),
                    OrderNo = reader.GetString(12)
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

internal sealed class CookingInstructionSlotSqlRow
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
}

internal sealed class CookingInstructionSearchSqlRow
{
    public long OrderTableId { get; set; }
    public string ItemName { get; set; } = "";
    public string NeedDate { get; set; } = "";
    public string SlotDisplay { get; set; } = "";
}

internal sealed class CookingInstructionLineHeaderRow
{
    public long OrderTableId { get; set; }
    public decimal RawOrdertableQty { get; set; }
    public string ParentItemcode { get; set; } = "";
    public string ParentItemname { get; set; } = "";
    public string Unit0Name { get; set; } = "";
    public string? ProcurementUnitName { get; set; }
    public string? IaStd { get; set; }
    public decimal? IaCar0 { get; set; }
    public decimal? IaCar1 { get; set; }
    public string SlotDisplay { get; set; } = "";
    public string WorkplaceNames { get; set; } = "";
    public DateOnly? NeedDate { get; set; }
    /// <summary>PDF 注番等: 受注ヘッダ ID（salesorderline があるとき）。無ければ ordertableid。</summary>
    public string OrderNo { get; set; } = "";
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
