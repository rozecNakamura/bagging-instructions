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
        var rows = await _db.Workcenters.AsNoTracking()
            .OrderBy(w => w.SortOrder ?? int.MaxValue)
            .ThenBy(w => w.WorkcenterName ?? "")
            .ToListAsync(ct);
        return rows.ConvertAll(w => new CookingInstructionWorkcenterOptionDto
        {
            Id = w.WorkcenterId ?? 0,
            Code = w.WorkcenterCode ?? "",
            Name = w.WorkcenterName ?? ""
        });
    }

    /// <summary>
    /// 指定納期の調理指示書対象オーダーに紐づく便の一覧（納期連動）。
    /// </summary>
    public async Task<List<CookingInstructionSlotOptionDto>> ListSlotsAsync(string needDate, CancellationToken ct = default)
    {
        var date = ParseYyyymmdd(needDate);
        if (!date.HasValue)
            throw new ArgumentException("納期はYYYYMMDD形式（8桁）で指定してください。", nameof(needDate));
        var needDateYyyymmdd = date.Value.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

        var rows = await _db.Database
            .SqlQuery<CookingInstructionSlotSqlRow>($@"
SELECT DISTINCT
  COALESCE(
    NULLIF(TRIM(COALESCE(SPLIT_PART(ot.productno, '|', 2), '')), ''),
    NULLIF(TRIM(COALESCE(SPLIT_PART(parent_ot.productno, '|', 2), '')), ''),
    NULLIF(TRIM(COALESCE(SPLIT_PART(gp_ot.productno, '|', 2), '')), ''),
    ''
  ) AS ""Code"",
  COALESCE(
    NULLIF(TRIM(ds.slotname), ''),
    NULLIF(TRIM(COALESCE(SPLIT_PART(ot.productno, '|', 2), '')), ''),
    NULLIF(TRIM(COALESCE(SPLIT_PART(parent_ot.productno, '|', 2), '')), ''),
    NULLIF(TRIM(COALESCE(SPLIT_PART(gp_ot.productno, '|', 2), '')), ''),
    ''
  ) AS ""Name""
FROM ordertable ot
INNER JOIN item i ON i.itemcode = ot.itemcode
LEFT JOIN ordertable parent_ot ON parent_ot.ordertableid = ot.parentordertableid
LEFT JOIN ordertable gp_ot ON gp_ot.ordertableid = parent_ot.parentordertableid
LEFT JOIN deliveryslot ds ON ds.slotcode = COALESCE(
    NULLIF(TRIM(COALESCE(SPLIT_PART(ot.productno, '|', 2), '')), ''),
    NULLIF(TRIM(COALESCE(SPLIT_PART(parent_ot.productno, '|', 2), '')), ''),
    NULLIF(TRIM(COALESCE(SPLIT_PART(gp_ot.productno, '|', 2), '')), ''),
    ''
  )
WHERE UPPER(TRIM(COALESCE(ot.ordertype, ''))) = 'MO'
  AND TO_CHAR(COALESCE(ot.needdate, ot.releasedate), 'YYYYMMDD') = {needDateYyyymmdd}
  AND LEFT(TRIM(COALESCE(ot.itemcode, '')), 2) <> '50'
  AND COALESCE(
    NULLIF(TRIM(COALESCE(SPLIT_PART(ot.productno, '|', 2), '')), ''),
    NULLIF(TRIM(COALESCE(SPLIT_PART(parent_ot.productno, '|', 2), '')), ''),
    NULLIF(TRIM(COALESCE(SPLIT_PART(gp_ot.productno, '|', 2), '')), ''),
    ''
  ) <> ''
ORDER BY 1
")
            .ToListAsync(ct);

        return rows
            .Where(r => !string.IsNullOrWhiteSpace(r.Code))
            .Select(r => new CookingInstructionSlotOptionDto
            {
                Code = r.Code ?? "",
                Name = r.Name ?? ""
            })
            .ToList();
    }

    public async Task<List<CookingInstructionClassification3OptionDto>> ListClassification3sAsync(CancellationToken ct = default)
    {
        var rows = await _db.Database
            .SqlQuery<CookingInstructionClass3SqlRow>($@"
SELECT
  TRIM(classification3code) AS ""Code"",
  COALESCE(NULLIF(TRIM(classification3name), ''), TRIM(classification3code)) AS ""Name""
FROM classification3
WHERE TRIM(COALESCE(classification3code, '')) <> ''
ORDER BY 1
")
            .ToListAsync(ct);

        return rows
            .Where(r => !string.IsNullOrWhiteSpace(r.Code))
            .Select(r => new CookingInstructionClassification3OptionDto
            {
                Code = r.Code ?? "",
                Name = r.Name ?? ""
            })
            .ToList();
    }

    /// <summary>
    /// 1 行 = 1 ordertable（MO）。納期は COALESCE(needdate, releasedate)。作業区・便・作業名は未選択なら絞り込まない。
    /// </summary>
    public async Task<List<CookingInstructionSearchRowDto>> SearchAsync(
        string needDate,
        IReadOnlyList<long>? workcenterIds,
        IReadOnlyList<string>? slotCodes,
        IReadOnlyList<string>? classification3Codes = null,
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
        var class3Codes = (classification3Codes ?? Array.Empty<string>())
            .Select(s => (s ?? "").Trim())
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var needDateYyyymmdd = date.Value.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

        var rows = await _db.Database
            .SqlQuery<CookingInstructionSearchSqlRow>($@"
SELECT
  ot.ordertableid AS ""OrderTableId"",
  TRIM(COALESCE(ot.itemcode, i.itemcode, '')) AS ""ItemCode"",
  COALESCE(i.itemname, '') AS ""ItemName"",
  TO_CHAR(
    COALESCE(ot.needdate, ot.releasedate),
    'YYYYMMDD'
  ) AS ""NeedDate"",
  COALESCE(
    NULLIF(TRIM(ds.slotname), ''),
    NULLIF(TRIM(COALESCE(SPLIT_PART(ot.productno, '|', 2), '')), ''),
    NULLIF(TRIM(COALESCE(SPLIT_PART(parent_ot.productno, '|', 2), '')), ''),
    NULLIF(TRIM(COALESCE(SPLIT_PART(gp_ot.productno, '|', 2), '')), ''),
    ''
  ) AS ""SlotDisplay""
FROM ordertable ot
INNER JOIN item i ON i.itemcode = ot.itemcode
LEFT JOIN ordertable parent_ot ON parent_ot.ordertableid = ot.parentordertableid
LEFT JOIN ordertable gp_ot ON gp_ot.ordertableid = parent_ot.parentordertableid
LEFT JOIN deliveryslot ds ON ds.slotcode = COALESCE(
    NULLIF(TRIM(COALESCE(SPLIT_PART(ot.productno, '|', 2), '')), ''),
    NULLIF(TRIM(COALESCE(SPLIT_PART(parent_ot.productno, '|', 2), '')), ''),
    NULLIF(TRIM(COALESCE(SPLIT_PART(gp_ot.productno, '|', 2), '')), ''),
    ''
  )
WHERE UPPER(TRIM(COALESCE(ot.ordertype, ''))) = 'MO'
  AND TO_CHAR(
        COALESCE(ot.needdate, ot.releasedate),
        'YYYYMMDD'
      ) = {needDateYyyymmdd}
  AND ({slots.Length} = 0 OR COALESCE(
        NULLIF(TRIM(COALESCE(SPLIT_PART(ot.productno, '|', 2), '')), ''),
        NULLIF(TRIM(COALESCE(SPLIT_PART(parent_ot.productno, '|', 2), '')), ''),
        NULLIF(TRIM(COALESCE(SPLIT_PART(gp_ot.productno, '|', 2), '')), ''),
        ''
      ) = ANY ({slots}))
  AND ({wcIds.Length} = 0 OR (
        EXISTS (
          SELECT 1 FROM itemworkcentermapping m3
          INNER JOIN workcenter wc ON wc.workcentercode = m3.workcentercode
          WHERE m3.itemcode = COALESCE(ot.itemcode, i.itemcode) AND wc.workcenterid = ANY ({wcIds})
        )
        OR EXISTS (
          SELECT 1 FROM workcenter wc_d
          WHERE wc_d.workcentercode = TRIM(COALESCE(ot.workcentercode, ''))
            AND wc_d.workcenterid = ANY ({wcIds})
        )
      ))
  AND ({class3Codes.Length} = 0 OR TRIM(COALESCE(i.classfication3code, '')) = ANY ({class3Codes}))
  AND LEFT(TRIM(COALESCE(ot.itemcode, '')), 2) <> '50'
ORDER BY i.itemname, COALESCE(
    NULLIF(TRIM(COALESCE(SPLIT_PART(ot.productno, '|', 2), '')), ''),
    NULLIF(TRIM(COALESCE(SPLIT_PART(parent_ot.productno, '|', 2), '')), ''),
    NULLIF(TRIM(COALESCE(SPLIT_PART(gp_ot.productno, '|', 2), '')), ''),
    ''
  ), ot.ordertableid
")
            .ToListAsync(ct);

        return rows.Select(r => new CookingInstructionSearchRowDto
        {
            OrderTableId = r.OrderTableId,
            ItemCode = r.ItemCode ?? "",
            ItemName = r.ItemName ?? "",
            NeedDate = FormatDateDisplay(r.NeedDate),
            SlotDisplay = r.SlotDisplay ?? ""
        }).ToList();
    }

    /// <summary>
    /// PDF 行生成。ids は ordertableid。
    /// 予定製造量: ordertable.qtyuni0 / qtyuni1..3（item.conversionvalue1..3 で単位0）優先、無ければ qty を単位0換算（ia.car1/car2/car3 / car0）。表示は qtyuni1＋手配単位または conversionvalue1 で手配表示。
    /// 子品目: 当該親の有効 BOM の子品目コード・名称・規格（<c>itemadditionalinformation.std</c>）。
    /// 予定使用量: 上記単位0換算後の製造数 × BOM（input/output/yield）による子所要量。
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

            var qtyU0 = CookingInstructionQuantity.ResolveParentQtyInUnit0(
                h.RawOrdertableQty,
                h.QtyUni0,
                h.QtyUni1,
                h.QtyUni2,
                h.QtyUni3,
                h.IaCar1,
                h.IaCar2,
                h.IaCar3,
                h.IaCar0,
                h.ConversionValue1,
                h.ConversionValue2,
                h.ConversionValue3);
            var (dispQty, dispUnit) = CookingInstructionQuantity.ParentPlannedQtyDisplay(
                qtyU0,
                h.QtyUni1,
                h.ProcurementUnitName,
                h.Unit0Name,
                h.ConversionValue1);
            var parentQtyDisplay = ReportQuantityFormatter.FormatCeilingQuantity(dispQty);
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
                    Standard = "",
                    ChildRequiredQtyDisplay = "",
                    ChildUnitName = "",
                    NeedDateDisplay = h.NeedDate?.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture) ?? "",
                    SlotDisplay = h.SlotDisplay,
                    WorkName = h.WorkName
                });
                continue;
            }

            foreach (var b in boms)
            {
                // 予定使用量: 単位0の製造数に対する BOM 子所要（手配単位表示の親数量とは独立）
                var childReqQty = PreparationBomQuantity.ComputeRequiredQty(qtyU0, b.InputQty, b.OutputQty, b.YieldPercent);
                var qtyDisplay = ReportQuantityFormatter.FormatCeilingQuantity(childReqQty);

                lines.Add(new CookingInstructionPdfLineModel
                {
                    OrderNo = h.OrderNo ?? "",
                    ParentItemCode = h.ParentItemcode,
                    ParentItemName = h.ParentItemname,
                    PlannedQuantityDisplay = parentQtyDisplay,
                    PlanUnitName = parentUnitName,
                    ChildItemCode = b.ChildItemcode,
                    ChildItemName = (b.ChildItemname ?? "").Trim(),
                    Standard = (b.ChildStd ?? "").Trim(),
                    ChildRequiredQtyDisplay = qtyDisplay,
                    ChildUnitName = (b.ChildUnitname ?? "").Trim(),
                    NeedDateDisplay = h.NeedDate?.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture) ?? "",
                    SlotDisplay = h.SlotDisplay,
                    WorkName = h.WorkName
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
                  ot.qtyuni0 AS ot_qtyuni0,
                  ot.qtyuni1 AS ot_qtyuni1,
                  ot.qtyuni2 AS ot_qtyuni2,
                  ot.qtyuni3 AS ot_qtyuni3,
                  COALESCE(ot.itemcode, i.itemcode, '') AS parent_itemcode,
                  COALESCE(i.itemname, '') AS parent_itemname,
                  COALESCE(u0.unitname, '') AS parent_u0_name,
                  u1.unitname AS procurement_u_name,
                  ia.car1 AS ia_car1,
                  ia.car2 AS ia_car2,
                  ia.car3 AS ia_car3,
                  ia.car0 AS ia_car0,
                  i.conversionvalue1 AS cv1,
                  i.conversionvalue2 AS cv2,
                  i.conversionvalue3 AS cv3,
                  COALESCE(
                    NULLIF(TRIM(ds.slotname), ''),
                    NULLIF(TRIM(COALESCE(SPLIT_PART(ot.productno, '|', 2), '')), ''),
                    NULLIF(TRIM(COALESCE(SPLIT_PART(parent_ot.productno, '|', 2), '')), ''),
                    NULLIF(TRIM(COALESCE(SPLIT_PART(gp_ot.productno, '|', 2), '')), ''),
                    ''
                  ) AS slot_display,
                  COALESCE(NULLIF(TRIM(c3.classification3name), ''), TRIM(COALESCE(i.classfication3code, ''))) AS work_name,
                  COALESCE(ot.needdate, ot.releasedate) AS need_date,
                  ot.ordertableid::text AS order_no_for_pdf
                FROM ordertable ot
                INNER JOIN item i ON i.itemcode = ot.itemcode
                LEFT JOIN itemadditionalinformation ia ON ia.itemcode = i.itemcode
                LEFT JOIN unit u0 ON u0.unitcode = i.unitcode0
                LEFT JOIN unit u1 ON u1.unitcode = i.unitcode1
                LEFT JOIN ordertable parent_ot ON parent_ot.ordertableid = ot.parentordertableid
                LEFT JOIN ordertable gp_ot ON gp_ot.ordertableid = parent_ot.parentordertableid
                LEFT JOIN deliveryslot ds ON ds.slotcode = COALESCE(
                    NULLIF(TRIM(COALESCE(SPLIT_PART(ot.productno, '|', 2), '')), ''),
                    NULLIF(TRIM(COALESCE(SPLIT_PART(parent_ot.productno, '|', 2), '')), ''),
                    NULLIF(TRIM(COALESCE(SPLIT_PART(gp_ot.productno, '|', 2), '')), ''),
                    ''
                  )
                LEFT JOIN classification3 c3 ON TRIM(c3.classification3code) = TRIM(i.classfication3code)
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
                    QtyUni0 = ReadDecimalNullable(reader, 2),
                    QtyUni1 = ReadDecimalNullable(reader, 3),
                    QtyUni2 = ReadDecimalNullable(reader, 4),
                    QtyUni3 = ReadDecimalNullable(reader, 5),
                    ParentItemcode = reader.GetString(6),
                    ParentItemname = reader.GetString(7),
                    Unit0Name = reader.GetString(8),
                    ProcurementUnitName = reader.IsDBNull(9) ? null : reader.GetString(9),
                    IaCar1 = ReadDecimalNullable(reader, 10),
                    IaCar2 = ReadDecimalNullable(reader, 11),
                    IaCar3 = ReadDecimalNullable(reader, 12),
                    IaCar0 = ReadDecimalNullable(reader, 13),
                    ConversionValue1 = ReadDecimalNullable(reader, 14),
                    ConversionValue2 = ReadDecimalNullable(reader, 15),
                    ConversionValue3 = ReadDecimalNullable(reader, 16),
                    SlotDisplay = reader.GetString(17),
                    WorkName = reader.GetString(18),
                    NeedDate = ReadDateNullable(reader, 19),
                    OrderNo = reader.GetString(20)
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
                  COALESCE(u.unitname, '') AS child_unitname,
                  COALESCE(BTRIM(COALESCE(ia.std::text, '')), '') AS child_std
                FROM bom b
                LEFT JOIN item ci ON TRIM(ci.itemcode) = TRIM(b.childitemcode)
                LEFT JOIN unit u ON u.unitcode = ci.unitcode0
                LEFT JOIN itemadditionalinformation ia ON TRIM(ia.itemcode) = TRIM(b.childitemcode)
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
                    ChildUnitname = reader.GetString(5),
                    ChildStd = reader.GetString(6)
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

internal sealed class CookingInstructionSlotSqlRow
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
}

internal sealed class CookingInstructionManufacturingRouteSqlRow
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
}

internal sealed class CookingInstructionClass3SqlRow
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
}

internal sealed class CookingInstructionSearchSqlRow
{
    public long OrderTableId { get; set; }
    public string ItemCode { get; set; } = "";
    public string ItemName { get; set; } = "";
    public string NeedDate { get; set; } = "";
    public string SlotDisplay { get; set; } = "";
}

internal sealed class CookingInstructionLineHeaderRow
{
    public long OrderTableId { get; set; }
    public decimal RawOrdertableQty { get; set; }
    public decimal? QtyUni0 { get; set; }
    public decimal? QtyUni1 { get; set; }
    public decimal? QtyUni2 { get; set; }
    public decimal? QtyUni3 { get; set; }
    public string ParentItemcode { get; set; } = "";
    public string ParentItemname { get; set; } = "";
    public string Unit0Name { get; set; } = "";
    public string? ProcurementUnitName { get; set; }
    public decimal? IaCar1 { get; set; }
    public decimal? IaCar2 { get; set; }
    public decimal? IaCar3 { get; set; }
    public decimal? IaCar0 { get; set; }
    public decimal? ConversionValue1 { get; set; }
    public decimal? ConversionValue2 { get; set; }
    public decimal? ConversionValue3 { get; set; }
    public string SlotDisplay { get; set; } = "";
    /// <summary>作業名: classfication3.classfication3name（GENRE01 タグに設定）。</summary>
    public string WorkName { get; set; } = "";
    public DateOnly? NeedDate { get; set; }
    /// <summary>PDF 注番: ordertable.ordertableid（文字列）。</summary>
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
    /// <summary>子品目の規格（<c>itemadditionalinformation.std</c>）。</summary>
    public string? ChildStd { get; set; }
}
