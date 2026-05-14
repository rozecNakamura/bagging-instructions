using System.Data;
using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using BaggingInstructions.Api.Core;
using BaggingInstructions.Api.DTOs;
using BaggingInstructions.Api.QueryResults;

namespace BaggingInstructions.Api.Services;

public class PreparationWorkService
{
    private readonly AppDbContext _db;

    public PreparationWorkService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<MiddleClassificationOptionDto>> ListMiddleClassificationsAsync(long majorClassificationId, CancellationToken ct = default)
    {
        var major = await _db.MajorClassifications.AsNoTracking()
            .FirstOrDefaultAsync(m => m.MajorClassificationId == majorClassificationId, ct);
        if (major == null || string.IsNullOrEmpty(major.MajorClassificationCode))
            return new List<MiddleClassificationOptionDto>();

        return await _db.MiddleClassifications.AsNoTracking()
            .Where(m => m.MajorClassificationCode == major.MajorClassificationCode)
            .OrderBy(m => m.MiddleClassificationCode ?? "")
            .Select(m => new MiddleClassificationOptionDto
            {
                Id = m.MiddleClassificationId,
                Code = m.MiddleClassificationCode ?? "",
                Name = m.MiddleClassificationName ?? "",
                MajorCode = m.MajorClassificationCode ?? ""
            })
            .ToListAsync(ct);
    }

    public async Task<List<PreparationWorkWorkcenterOptionDto>> ListWorkcentersAsync(CancellationToken ct = default)
    {
        var rows = await _db.Workcenters.AsNoTracking()
            .OrderBy(w => w.SortOrder ?? int.MaxValue)
            .ThenBy(w => w.WorkcenterName ?? "")
            .ToListAsync(ct);
        return rows.ConvertAll(w => new PreparationWorkWorkcenterOptionDto
        {
            Id = w.WorkcenterId ?? 0,
            Name = w.WorkcenterName ?? ""
        });
    }

    public async Task<List<PreparationWorkWarehouseOptionDto>> ListWarehousesAsync(CancellationToken ct = default)
    {
        var rows = await _db.Warehouses.AsNoTracking()
            .OrderBy(w => w.WarehouseCode ?? "")
            .ThenBy(w => w.WarehouseName ?? "")
            .ToListAsync(ct);
        return rows
            .Select(w => new PreparationWorkWarehouseOptionDto
            {
                Id = w.WarehouseId,
                Code = w.WarehouseCode ?? "",
                Name = w.WarehouseName ?? ""
            })
            .ToList();
    }

    /// <summary>
    /// 指定納期のオーダに紐づく製造便（<c>salesorderlineaddinfo.addinfo03</c>）の一覧。コード順。
    /// </summary>
    public async Task<List<PreparationWorkManufacturingRouteOptionDto>> ListManufacturingRoutesForNeedDateAsync(
        string delvedt,
        CancellationToken ct = default)
    {
        var date = ParseYyyymmdd(delvedt);
        if (!date.HasValue)
            throw new ArgumentException("納期はYYYYMMDD形式（8桁）で指定してください。", nameof(delvedt));

        var rows = await _db.Database
            .SqlQuery<PreparationWorkManufacturingRouteSqlRow>($@"
SELECT
  TRIM(COALESCE(a.addinfo03, '')) AS ""Code"",
  COALESCE(MAX(TRIM(COALESCE(a.addinfo03name, ''))), '') AS ""Name""
FROM ordertable ot
LEFT JOIN salesorderline sol ON sol.salesorderlineid = ot.salesorderlineid
INNER JOIN salesorderlineaddinfo a ON a.salesorderlineid = sol.salesorderlineid
WHERE COALESCE(ot.needdate, sol.planneddeliverydate) = {date.Value}
  AND TRIM(COALESCE(a.addinfo03, '')) <> ''
GROUP BY TRIM(COALESCE(a.addinfo03, ''))
ORDER BY TRIM(COALESCE(a.addinfo03, ''))
")
            .ToListAsync(ct);

        return rows
            .Where(r => !string.IsNullOrWhiteSpace(r.Code))
            .Select(r => new PreparationWorkManufacturingRouteOptionDto
            {
                Code = r.Code ?? "",
                Name = string.IsNullOrWhiteSpace(r.Name) ? (r.Code ?? "") : r.Name!
            })
            .ToList();
    }

    public async Task<List<PreparationWorkGroupDto>> SearchGroupsAsync(
        string delvedt,
        IReadOnlyList<string> manufacturingRouteCodes,
        string? itemcd,
        long? majorClassificationId,
        long? middleClassificationId,
        IReadOnlyList<long> workcenterIds,
        IReadOnlyList<long> warehouseIds,
        CancellationToken ct = default)
    {
        var date = ParseYyyymmdd(delvedt);
        if (!date.HasValue)
            throw new ArgumentException("納期はYYYYMMDD形式（8桁）で指定してください。", nameof(delvedt));

        var mfgRoutes = (manufacturingRouteCodes ?? Array.Empty<string>())
            .Select(s => (s ?? "").Trim())
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var wcIds = (workcenterIds ?? Array.Empty<long>()).Where(id => id > 0).Distinct().ToArray();
        var whIds = (warehouseIds ?? Array.Empty<long>()).Where(id => id > 0).Distinct().ToArray();

        var itemF = itemcd?.Trim() ?? "";

        // major / middle は ID ではなく「コード」で SQL フィルタする（NULL パラメータ型不明エラー回避）
        string majorCodeFilter = "";
        string middleCodeFilter = "";

        if (majorClassificationId is long majId and > 0)
        {
            var major = await _db.MajorClassifications.AsNoTracking()
                .FirstOrDefaultAsync(m => m.MajorClassificationId == majId, ct);
            if (major?.MajorClassificationCode is { Length: > 0 } code)
                majorCodeFilter = code;
        }

        if (middleClassificationId is long midId and > 0)
        {
            var middle = await _db.MiddleClassifications.AsNoTracking()
                .FirstOrDefaultAsync(m => m.MiddleClassificationId == midId, ct);
            if (middle?.MiddleClassificationCode is { Length: > 0 } code)
                middleCodeFilter = code;
        }

        var rows = await _db.Database
            .SqlQuery<PreparationWorkGroupSqlRow>($@"
SELECT
  TO_CHAR(COALESCE(ot.needdate, sol.planneddeliverydate), 'YYYYMMDD') AS ""Delvedt"",
  COALESCE(mc.majorclassificationcode, '') AS ""MajorCode"",
  COALESCE(mc.majorclassificationname, '') AS ""MajorName"",
  COALESCE(mid.middleclassificationcode, '') AS ""MiddleCode"",
  COALESCE(mid.middleclassificationname, '') AS ""MiddleName"",
  COUNT(*)::int AS ""LineCount""
FROM ordertable ot
LEFT JOIN salesorderline sol ON sol.salesorderlineid = ot.salesorderlineid
INNER JOIN item i ON TRIM(BOTH FROM i.itemcode) = TRIM(BOTH FROM COALESCE(NULLIF(TRIM(BOTH FROM sol.itemcode), ''), ot.itemcode))
LEFT JOIN majorclassification mc ON mc.majorclassificationcode = i.majorclassficationcode
LEFT JOIN middleclassification mid ON mid.majorclassificationcode = i.majorclassficationcode
  AND mid.middleclassificationcode = i.middleclassficationcode
WHERE COALESCE(ot.needdate, sol.planneddeliverydate) = {date.Value}
  AND ({mfgRoutes.Length} = 0 OR EXISTS (
        SELECT 1 FROM salesorderlineaddinfo sai
        WHERE sai.salesorderlineid = sol.salesorderlineid
          AND TRIM(COALESCE(sai.addinfo03, '')) = ANY ({mfgRoutes})
      ))
  AND ({wcIds.Length} = 0 OR EXISTS (
        SELECT 1 FROM itemworkcentermapping m3
        INNER JOIN workcenter wc ON wc.workcentercode = m3.workcentercode
        WHERE m3.itemcode = TRIM(BOTH FROM COALESCE(NULLIF(TRIM(BOTH FROM sol.itemcode), ''), ot.itemcode))
          AND wc.workcenterid = ANY ({wcIds})
      ))
  AND ({whIds.Length} = 0 OR EXISTS (
        SELECT 1 FROM warehouses wh_f
        WHERE wh_f.warehouseid = ANY ({whIds})
          AND TRIM(COALESCE(wh_f.warehousecode, '')) = TRIM(COALESCE(i.warehousecode, ''))
      ))
  AND ({itemF} = '' OR i.itemcode ILIKE '%' || {itemF} || '%')
  AND ({majorCodeFilter} = '' OR mc.majorclassificationcode = {majorCodeFilter})
  AND ({middleCodeFilter} = '' OR mid.middleclassificationcode = {middleCodeFilter})
GROUP BY
  TO_CHAR(COALESCE(ot.needdate, sol.planneddeliverydate), 'YYYYMMDD'),
  mc.majorclassificationcode,
  mc.majorclassificationname,
  mid.middleclassificationcode,
  mid.middleclassificationname
ORDER BY ""MajorCode"", ""MiddleCode""
")
            .ToListAsync(ct);

        return rows.Select(r => new PreparationWorkGroupDto
        {
            Delvedt = r.Delvedt,
            MajorClassificationName = r.MajorName,
            MiddleClassificationName = r.MiddleName,
            LineCount = r.LineCount,
            Key = new PreparationWorkGroupKeyDto
            {
                Delvedt = r.Delvedt,
                MajorClassificationCode = string.IsNullOrEmpty(r.MajorCode) ? null : r.MajorCode,
                MiddleClassificationCode = string.IsNullOrEmpty(r.MiddleCode) ? null : r.MiddleCode
            }
        }).ToList();
    }

    public async Task<IReadOnlyList<long>> ResolveLineIdsAsync(
        PreparationWorkFilterRequestDto filter,
        IReadOnlyList<PreparationWorkGroupKeyDto> groupKeys,
        CancellationToken ct = default)
    {
        if (groupKeys == null || groupKeys.Count == 0)
            return Array.Empty<long>();

        var date = ParseYyyymmdd(filter.Delvedt ?? "");
        if (!date.HasValue)
            throw new ArgumentException("納期はYYYYMMDD形式（8桁）で指定してください。", nameof(filter.Delvedt));

        var mfgRoutes = (filter.ManufacturingRouteCodes ?? new List<string>())
            .Select(s => (s ?? "").Trim())
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var wcIds = (filter.WorkcenterIds ?? new List<long>()).Where(id => id > 0).Distinct().ToArray();
        var whIds = (filter.WarehouseIds ?? new List<long>()).Where(id => id > 0).Distinct().ToArray();

        var itemF = filter.Itemcd?.Trim() ?? "";

        var all = new HashSet<long>();
        foreach (var key in groupKeys)
        {
            var maj = key.MajorClassificationCode ?? "";
            var mid = key.MiddleClassificationCode ?? "";
            var ids = await _db.Database
                .SqlQuery<PreparationWorkLineIdSqlRow>($@"
SELECT ot.ordertableid AS ""Ordertableid""
FROM ordertable ot
LEFT JOIN salesorderline sol ON sol.salesorderlineid = ot.salesorderlineid
INNER JOIN item i ON TRIM(BOTH FROM i.itemcode) = TRIM(BOTH FROM COALESCE(NULLIF(TRIM(BOTH FROM sol.itemcode), ''), ot.itemcode))
LEFT JOIN majorclassification mc ON mc.majorclassificationcode = i.majorclassficationcode
LEFT JOIN middleclassification midt ON midt.majorclassificationcode = i.majorclassficationcode
  AND midt.middleclassificationcode = i.middleclassficationcode
WHERE COALESCE(ot.needdate, sol.planneddeliverydate) = {date.Value}
  AND TO_CHAR(COALESCE(ot.needdate, sol.planneddeliverydate), 'YYYYMMDD') = {key.Delvedt}
  AND COALESCE(mc.majorclassificationcode, '') = {maj}
  AND COALESCE(midt.middleclassificationcode, '') = {mid}
  AND ({mfgRoutes.Length} = 0 OR EXISTS (
        SELECT 1 FROM salesorderlineaddinfo sai
        WHERE sai.salesorderlineid = sol.salesorderlineid
          AND TRIM(COALESCE(sai.addinfo03, '')) = ANY ({mfgRoutes})
      ))
  AND ({wcIds.Length} = 0 OR EXISTS (
        SELECT 1 FROM itemworkcentermapping m3
        INNER JOIN workcenter wc ON wc.workcentercode = m3.workcentercode
        WHERE m3.itemcode = TRIM(BOTH FROM COALESCE(NULLIF(TRIM(BOTH FROM sol.itemcode), ''), ot.itemcode))
          AND wc.workcenterid = ANY ({wcIds})
      ))
  AND ({whIds.Length} = 0 OR EXISTS (
        SELECT 1 FROM warehouses wh_f
        WHERE wh_f.warehouseid = ANY ({whIds})
          AND TRIM(COALESCE(wh_f.warehousecode, '')) = TRIM(COALESCE(i.warehousecode, ''))
      ))
  AND ({itemF} = '' OR i.itemcode ILIKE '%' || {itemF} || '%')
  AND ot.ordertableid IS NOT NULL
")
                .ToListAsync(ct);
            foreach (var row in ids)
                all.Add(row.Ordertableid);
        }

        return all.OrderBy(x => x).ToList();
    }

    public async Task<List<PreparationCsvRow>> BuildCsvRowsAsync(IReadOnlyList<long> lineIds, CancellationToken ct = default)
    {
        if (lineIds.Count == 0)
            return new List<PreparationCsvRow>();

        var headers = await FetchLineHeadersAsync(lineIds, ct);
        var bomCache = new Dictionary<string, List<PreparationBomSqlRow>>(StringComparer.Ordinal);

        var rows = new List<PreparationCsvRow>();
        foreach (var h in headers)
        {
            var asof = h.PlannedDeliveryDate ?? h.NeedDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
            if (!bomCache.TryGetValue(h.ParentItemcode, out var boms))
            {
                boms = await FetchBomsForParentAsync(h.ParentItemcode, asof, ct);
                bomCache[h.ParentItemcode] = boms;
            }

            if (boms.Count == 0)
            {
                rows.Add(new PreparationCsvRow
                {
                    SterilizationTemperatureRange = "",
                    WorkplaceName = h.WorkplaceNames,
                    DeliveryDate = h.NeedDate?.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture) ?? "",
                    Slot = h.SlotDisplay,
                    SmallClassName = h.MinorClassName,
                    OrderNo = h.Ordertableid.ToString(CultureInfo.InvariantCulture),
                    ParentItemcode = h.ParentItemcode,
                    ParentItemname = h.ParentItemname,
                    ChildItemcode = "",
                    ChildItemname = "",
                    Quantity = "",
                    Unit = ""
                });
                continue;
            }

            foreach (var b in boms)
            {
                var qty = PreparationBomQuantity.ComputeRequiredQty(h.MfgQty, b.InputQty, b.OutputQty, b.YieldPercent);
                var qtyDisplay = ReportQuantityFormatter.FormatCeilingQuantity(qty);
                rows.Add(new PreparationCsvRow
                {
                    SterilizationTemperatureRange = b.ChildSteriTempRange ?? "",
                    WorkplaceName = h.WorkplaceNames,
                    DeliveryDate = h.NeedDate?.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture) ?? "",
                    Slot = h.SlotDisplay,
                    SmallClassName = h.MinorClassName,
                    OrderNo = h.Ordertableid.ToString(CultureInfo.InvariantCulture),
                    ParentItemcode = h.ParentItemcode,
                    ParentItemname = h.ParentItemname,
                    ChildItemcode = b.ChildItemcode,
                    ChildItemname = b.ChildItemname ?? "",
                    Quantity = qtyDisplay,
                    Unit = b.ChildUnitname ?? ""
                });
            }
        }

        return PreparationWorkReportSort.SortCsvRows(rows);
    }

    public async Task<List<PreparationPdfLineModel>> BuildPdfLineModelsAsync(IReadOnlyList<long> lineIds, CancellationToken ct = default)
    {
        if (lineIds.Count == 0)
            return new List<PreparationPdfLineModel>();

        var headers = await FetchLineHeadersAsync(lineIds, ct);
        var bomCache = new Dictionary<string, List<PreparationBomSqlRow>>(StringComparer.Ordinal);

        var lines = new List<PreparationPdfLineModel>();
        foreach (var h in headers)
        {
            var asof = h.PlannedDeliveryDate ?? h.NeedDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
            if (!bomCache.TryGetValue(h.ParentItemcode, out var boms))
            {
                boms = await FetchBomsForParentAsync(h.ParentItemcode, asof, ct);
                bomCache[h.ParentItemcode] = boms;
            }

            var hasProductNo = !string.IsNullOrWhiteSpace(h.ProductNo);
            if (boms.Count == 0)
            {
                lines.Add(new PreparationPdfLineModel
                {
                    MiddleClassificationName = h.MiddleClassName,
                    DateDisplay = h.NeedDate?.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture) ?? "",
                    WorkplaceName = h.WorkplaceNames,
                    WorkplaceCode = h.WorkplaceCode,
                    ManufacturingRouteCode = h.ManufacturingRouteCode,
                    MiddleClassificationCode = h.MiddleClassificationCode,
                    OrderNo = h.Ordertableid.ToString(CultureInfo.InvariantCulture),
                    ParentItemcode = h.ParentItemcode,
                    ParentItemname = h.ParentItemname,
                    ChildItemcode = "",
                    ChildItemname = "",
                    Standard = "",
                    TemperatureRange = "",
                    Quantity = "",
                    Unit = "",
                    WarehouseName = "",
                    HasProductNo = hasProductNo
                });
                continue;
            }

            foreach (var b in boms)
            {
                var qty = PreparationBomQuantity.ComputeRequiredQty(h.MfgQty, b.InputQty, b.OutputQty, b.YieldPercent);
                var qtyDisplay = ReportQuantityFormatter.FormatCeilingQuantity(qty);

                lines.Add(new PreparationPdfLineModel
                {
                    MiddleClassificationName = h.MiddleClassName,
                    DateDisplay = h.NeedDate?.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture) ?? "",
                    WorkplaceName = h.WorkplaceNames,
                    WorkplaceCode = h.WorkplaceCode,
                    ManufacturingRouteCode = h.ManufacturingRouteCode,
                    MiddleClassificationCode = h.MiddleClassificationCode,
                    OrderNo = h.Ordertableid.ToString(CultureInfo.InvariantCulture),
                    ParentItemcode = h.ParentItemcode,
                    ParentItemname = h.ParentItemname,
                    ChildItemcode = b.ChildItemcode,
                    ChildItemname = b.ChildItemname ?? "",
                    Standard = b.ChildStd ?? "",
                    TemperatureRange = b.ChildSteriTempRange ?? "",
                    Quantity = qtyDisplay,
                    Unit = b.ChildUnitname ?? "",
                    WarehouseName = b.ChildWarehouseName ?? "",
                    HasProductNo = hasProductNo
                });
            }
        }

        return lines;
    }

    public static byte[] WriteCsvUtf8Bom(IReadOnlyList<PreparationCsvRow> rows)
    {
        static string Esc(string? s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.Contains('"') || s.Contains(',') || s.Contains('\n') || s.Contains('\r'))
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }

        var sb = new StringBuilder();
        sb.AppendLine("職場名,日付,製造便,分類名,注番,親品目コード,親品目,子品目コード,子品目,数量,単位");
        foreach (var r in rows)
        {
            sb.Append(Esc(r.WorkplaceName)).Append(',')
                .Append(Esc(r.DeliveryDate)).Append(',')
                .Append(Esc(r.Slot)).Append(',')
                .Append(Esc(r.SmallClassName)).Append(',')
                .Append(Esc(r.OrderNo)).Append(',')
                .Append(Esc(r.ParentItemcode)).Append(',')
                .Append(Esc(r.ParentItemname)).Append(',')
                .Append(Esc(r.ChildItemcode)).Append(',')
                .Append(Esc(r.ChildItemname)).Append(',')
                .Append(Esc(r.Quantity)).Append(',')
                .Append(Esc(r.Unit))
                .AppendLine();
        }

        var preamble = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(sb.ToString());
        var result = new byte[preamble.Length + body.Length];
        Buffer.BlockCopy(preamble, 0, result, 0, preamble.Length);
        Buffer.BlockCopy(body, 0, result, preamble.Length, body.Length);
        return result;
    }

    /// <summary>
    /// date 列が DateTime で返る場合があるため、DateOnly への読み取りを統一する。
    /// </summary>
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

    private async Task<List<PreparationLineHeaderRow>> FetchLineHeadersAsync(IReadOnlyList<long> lineIds, CancellationToken ct)
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
                  COALESCE(ot.ordertableid, 0),
                  COALESCE(sol.salesorderlineid, 0),
                  COALESCE(sol.salesorderid, 0),
                  COALESCE(ot.qty, sol.quantity) AS mfg_qty,
                  i.itemcode AS parent_itemcode,
                  COALESCE(i.itemname, '') AS parent_itemname,
                  COALESCE(mn.minorclassificationname, '') AS minor_class_name,
                  COALESCE(mid.middleclassificationname, '') AS middle_class_name,
                  COALESCE(ds.slotname, ds.slotcode, '') AS slot_display,
                  COALESCE(
                    NULLIF(TRIM(BOTH FROM wc_ord.workcentername), ''),
                    (SELECT string_agg(DISTINCT wc_map.workcentername, '、' ORDER BY wc_map.workcentername)
                     FROM itemworkcentermapping m2
                     INNER JOIN workcenter wc_map ON wc_map.workcentercode = m2.workcentercode
                     WHERE m2.itemcode = i.itemcode),
                    ''
                  ) AS workplace_names,
                  COALESCE(sol.planneddeliverydate, ot.releasedate) AS planned_delivery,
                  COALESCE(ot.needdate, sol.planneddeliverydate) AS need_date,
                  COALESCE(
                    NULLIF(TRIM(BOTH FROM wc_ord.workcentercode), ''),
                    (SELECT string_agg(DISTINCT TRIM(BOTH FROM wc_map.workcentercode), '、' ORDER BY TRIM(BOTH FROM wc_map.workcentercode))
                     FROM itemworkcentermapping m2
                     INNER JOIN workcenter wc_map ON wc_map.workcentercode = m2.workcentercode
                     WHERE m2.itemcode = i.itemcode),
                    ''
                  ) AS workplace_code,
                  TRIM(COALESCE(sai.addinfo03, '')) AS manufacturing_route_code,
                  COALESCE(mid.middleclassificationcode, '') AS middle_class_code,
                  ot.productno
                FROM ordertable ot
                LEFT JOIN workcenter wc_ord ON (
                     wc_ord.workcentercode = TRIM(BOTH FROM ot.workcentercode)
                  OR wc_ord.workcenterid::text = TRIM(BOTH FROM ot.workcentercode)
                )
                LEFT JOIN salesorderline sol ON sol.salesorderlineid = ot.salesorderlineid
                LEFT JOIN salesorderlineaddinfo sai ON sai.salesorderlineid = sol.salesorderlineid
                INNER JOIN item i ON TRIM(BOTH FROM i.itemcode) = TRIM(BOTH FROM COALESCE(NULLIF(TRIM(BOTH FROM sol.itemcode), ''), ot.itemcode))
                LEFT JOIN minorclassification mn ON mn.majorclassificationcode = i.majorclassficationcode
                  AND mn.middleclassificationcode = i.middleclassficationcode
                  AND mn.minorclassificationcode = i.minorclassficationcode
                LEFT JOIN middleclassification mid ON mid.majorclassificationcode = i.majorclassficationcode
                  AND mid.middleclassificationcode = i.middleclassficationcode
                LEFT JOIN deliveryslot ds ON ds.slotcode = COALESCE(sol.slotcode, ot.slotcode)
                WHERE ot.ordertableid = ANY(@ids)
                ORDER BY ot.ordertableid
                """, conn);
            cmd.Parameters.Add(new NpgsqlParameter("ids", NpgsqlDbType.Bigint | NpgsqlDbType.Array)
            {
                Value = lineIds.ToArray()
            });
            var list = new List<PreparationLineHeaderRow>();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                list.Add(new PreparationLineHeaderRow
                {
                    Ordertableid = reader.GetInt64(0),
                    Salesorderlineid = reader.GetInt64(1),
                    Salesorderid = reader.GetInt64(2),
                    MfgQty = reader.GetDecimal(3),
                    ParentItemcode = reader.GetString(4),
                    ParentItemname = reader.GetString(5),
                    MinorClassName = reader.GetString(6),
                    MiddleClassName = reader.GetString(7),
                    SlotDisplay = reader.GetString(8),
                    WorkplaceNames = reader.GetString(9),
                    PlannedDeliveryDate = ReadDateNullable(reader, 10),
                    NeedDate = ReadDateNullable(reader, 11),
                    WorkplaceCode = reader.GetString(12),
                    ManufacturingRouteCode = reader.GetString(13),
                    MiddleClassificationCode = reader.GetString(14),
                    ProductNo = reader.IsDBNull(15) ? null : reader.GetString(15)
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

    private async Task<List<PreparationBomSqlRow>> FetchBomsForParentAsync(string parentItemcode, DateOnly asOf, CancellationToken ct)
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
                  COALESCE(BTRIM(COALESCE(ia.std::text, '')), '') AS child_std,
                  CASE
                    WHEN ia.steritemprange IS NULL THEN ''
                    ELSE TO_CHAR(ia.steritemprange, 'FM999999990.###')
                  END AS child_steritemprange,
                  COALESCE(wh_child.warehousename, wh_child.warehousecode, '') AS child_warehouse_name
                FROM bom b
                LEFT JOIN item ci ON TRIM(ci.itemcode) = TRIM(b.childitemcode)
                LEFT JOIN warehouses wh_child ON wh_child.warehousecode = ci.warehousecode
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
            var list = new List<PreparationBomSqlRow>();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                list.Add(new PreparationBomSqlRow
                {
                    ChildItemcode = reader.GetString(0),
                    InputQty = reader.GetDecimal(1),
                    OutputQty = reader.GetDecimal(2),
                    YieldPercent = reader.GetDecimal(3),
                    ChildItemname = reader.GetString(4),
                    ChildUnitname = reader.GetString(5),
                    ChildStd = reader.GetString(6),
                    ChildSteriTempRange = reader.GetString(7),
                    ChildWarehouseName = reader.GetString(8)
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

    private static DateOnly? ParseYyyymmdd(string? s)
    {
        if (string.IsNullOrEmpty(s) || s.Length != 8) return null;
        if (int.TryParse(s.AsSpan(0, 4), out var y) && int.TryParse(s.AsSpan(4, 2), out var m) && int.TryParse(s.AsSpan(6, 2), out var d))
            return new DateOnly(y, m, d);
        return null;
    }
}

public sealed class PreparationCsvRow
{
    /// <summary>子品目の殺菌温度レンジ（ソートキー用。CSV の列には出力しない）。</summary>
    public string SterilizationTemperatureRange { get; set; } = "";

    public string WorkplaceName { get; set; } = "";
    public string DeliveryDate { get; set; } = "";
    public string Slot { get; set; } = "";
    public string SmallClassName { get; set; } = "";
    public string OrderNo { get; set; } = "";
    public string ParentItemcode { get; set; } = "";
    public string ParentItemname { get; set; } = "";
    public string ChildItemcode { get; set; } = "";
    public string ChildItemname { get; set; } = "";
    public string Quantity { get; set; } = "";
    public string Unit { get; set; } = "";
}

public sealed class PreparationPdfLineModel
{
    public string MiddleClassificationName { get; set; } = "";
    public string DateDisplay { get; set; } = "";
    public string WorkplaceName { get; set; } = "";
    public string WorkplaceCode { get; set; } = "";
    public string ManufacturingRouteCode { get; set; } = "";
    public string MiddleClassificationCode { get; set; } = "";
    public string DisplayOrder { get; set; } = "";
    public string OrderNo { get; set; } = "";
    public string ParentItemcode { get; set; } = "";
    public string ParentItemname { get; set; } = "";
    public string ChildItemcode { get; set; } = "";
    public string ChildItemname { get; set; } = "";
    public string Standard { get; set; } = "";
    public string TemperatureRange { get; set; } = "";
    public string Quantity { get; set; } = "";
    public string Unit { get; set; } = "";
    public string WarehouseName { get; set; } = "";
    /// <summary>ordertable.productno が存在するか（true=袋品, false=その他）。</summary>
    public bool HasProductNo { get; set; }
}

internal sealed class PreparationLineHeaderRow
{
    public long Ordertableid { get; set; }
    public long Salesorderlineid { get; set; }
    public long Salesorderid { get; set; }
    public decimal MfgQty { get; set; }
    public string ParentItemcode { get; set; } = "";
    public string ParentItemname { get; set; } = "";
    public string MinorClassName { get; set; } = "";
    public string MiddleClassName { get; set; } = "";
    public string SlotDisplay { get; set; } = "";
    public string WorkplaceNames { get; set; } = "";
    public string WorkplaceCode { get; set; } = "";
    public string ManufacturingRouteCode { get; set; } = "";
    public string MiddleClassificationCode { get; set; } = "";
    public DateOnly? PlannedDeliveryDate { get; set; }
    /// <summary>納期（CSV/帳票に表示する日付）。ordertable.needdate 優先、無ければ planneddeliverydate。</summary>
    public DateOnly? NeedDate { get; set; }
    /// <summary>ordertable.productno（null の場合はその他、値があれば袋品）。</summary>
    public string? ProductNo { get; set; }
}

internal sealed class PreparationBomSqlRow
{
    public string ChildItemcode { get; set; } = "";
    public decimal InputQty { get; set; }
    public decimal OutputQty { get; set; }
    public decimal YieldPercent { get; set; }
    public string? ChildItemname { get; set; }
    public string? ChildUnitname { get; set; }
    /// <summary>子品目の規格（<c>itemadditionalinformation.std</c> のみ）。</summary>
    public string? ChildStd { get; set; }
    /// <summary>子品目の殺菌温度レンジ（<c>itemadditionalinformation.steritemprange</c>）。</summary>
    public string? ChildSteriTempRange { get; set; }
    /// <summary>子品目の保管倉庫名（<c>item.warehousecode</c> → <c>warehouses.warehousename</c>）。</summary>
    public string? ChildWarehouseName { get; set; }
}
