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

    public async Task<List<MiddleClassificationOptionDto>> ListMiddleClassificationsAsync(long[] majorClassificationIds, CancellationToken ct = default)
    {
        var majIds = (majorClassificationIds ?? Array.Empty<long>()).Where(id => id > 0).Distinct().ToArray();
        if (majIds.Length == 0)
            return new List<MiddleClassificationOptionDto>();

        var majorCodes = await _db.MajorClassifications.AsNoTracking()
            .Where(m => majIds.Contains(m.MajorClassificationId))
            .Select(m => m.MajorClassificationCode)
            .Where(c => c != null && c.Length > 0)
            .ToListAsync(ct);

        var codes = majorCodes.OfType<string>().ToList();
        if (codes.Count == 0)
            return new List<MiddleClassificationOptionDto>();

        return await _db.MiddleClassifications.AsNoTracking()
            .Where(m => m.MajorClassificationCode != null && codes.Contains(m.MajorClassificationCode))
            .OrderBy(m => m.MajorClassificationCode ?? "")
            .ThenBy(m => m.MiddleClassificationCode ?? "")
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
            Code = w.WorkcenterCode ?? "",
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
  sc.slotcode AS ""Code"",
  COALESCE(NULLIF(TRIM(ds.slotname), ''), NULLIF(sc.slotcode, ''), '') AS ""Name""
FROM ordertable ot
LEFT JOIN salesorderline sol ON sol.salesorderlineid = ot.salesorderlineid
LEFT JOIN ordertable parent_ot ON parent_ot.ordertableid = ot.parentordertableid
LEFT JOIN ordertable gp_ot     ON gp_ot.ordertableid     = parent_ot.parentordertableid
CROSS JOIN LATERAL (
  SELECT COALESCE(
    -- 自オーダーが製番品ならその便
    NULLIF(TRIM(COALESCE(CASE WHEN CARDINALITY(STRING_TO_ARRAY(ot.productno, '|')) >= 5 THEN SPLIT_PART(ot.productno, '|', 3) ELSE SPLIT_PART(ot.productno, '|', 2) END, '')), ''),
    -- [SLOT-CHAIN] 親を遡るのは親自身が製番品（productno非空）のときのみ。
    -- 自オーダーにproductnoが無い＝MRP品で、親もproductno空なら製造便なし（''）として扱う。
    CASE WHEN COALESCE(TRIM(parent_ot.productno), '') <> ''
         THEN NULLIF(TRIM(COALESCE(CASE WHEN CARDINALITY(STRING_TO_ARRAY(parent_ot.productno, '|')) >= 5 THEN SPLIT_PART(parent_ot.productno, '|', 3) ELSE SPLIT_PART(parent_ot.productno, '|', 2) END, '')), '')
    END,
    CASE WHEN COALESCE(TRIM(parent_ot.productno), '') <> '' AND COALESCE(TRIM(gp_ot.productno), '') <> ''
         THEN NULLIF(TRIM(COALESCE(CASE WHEN CARDINALITY(STRING_TO_ARRAY(gp_ot.productno, '|')) >= 5 THEN SPLIT_PART(gp_ot.productno, '|', 3) ELSE SPLIT_PART(gp_ot.productno, '|', 2) END, '')), '')
    END,
    ''
  ) AS slotcode
) sc
LEFT JOIN deliveryslot ds ON ds.slotcode = sc.slotcode
WHERE COALESCE(ot.releasedate, sol.planneddeliverydate) = {date.Value}
  AND UPPER(TRIM(COALESCE(ot.ordertype, ''))) = 'MO'
  AND sc.slotcode <> ''
  -- [DEDUP-productno] 同一品目・同一productno（同一実効日付）は最新ordertableidのみ採用（MRP重複対策）
  AND (
    COALESCE(TRIM(ot.productno), '') = ''
    OR ot.ordertableid = (
      SELECT MAX(o2.ordertableid) FROM ordertable o2
      WHERE TRIM(o2.productno) = TRIM(ot.productno)
        AND TRIM(o2.itemcode) = TRIM(ot.itemcode)
        AND COALESCE(o2.releasedate, o2.needdate) IS NOT DISTINCT FROM COALESCE(ot.releasedate, ot.needdate)
    )
  )
GROUP BY sc.slotcode, TRIM(ds.slotname)
ORDER BY 1
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
        long[]? majorClassificationIds,
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
        string[] majorCodes = Array.Empty<string>();
        string middleCodeFilter = "";

        var majIds = (majorClassificationIds ?? Array.Empty<long>()).Where(id => id > 0).Distinct().ToArray();
        if (majIds.Length > 0)
        {
            var codes = await _db.MajorClassifications.AsNoTracking()
                .Where(m => majIds.Contains(m.MajorClassificationId))
                .Select(m => m.MajorClassificationCode)
                .ToListAsync(ct);
            majorCodes = codes.Where(c => !string.IsNullOrEmpty(c)).Select(c => c!).ToArray();
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
  TO_CHAR(COALESCE(ot.releasedate, sol.planneddeliverydate), 'YYYYMMDD') AS ""Delvedt"",
  COALESCE(mc.majorclassificationcode, '') AS ""MajorCode"",
  COALESCE(mc.majorclassificationname, '') AS ""MajorName"",
  COALESCE(mid.middleclassificationcode, '') AS ""MiddleCode"",
  COALESCE(mid.middleclassificationname, '') AS ""MiddleName"",
  COUNT(*)::int AS ""LineCount""
FROM ordertable ot
LEFT JOIN salesorderline sol ON sol.salesorderlineid = ot.salesorderlineid
LEFT JOIN ordertable parent_ot ON parent_ot.ordertableid = ot.parentordertableid
INNER JOIN item i ON TRIM(BOTH FROM i.itemcode) = TRIM(BOTH FROM COALESCE(NULLIF(TRIM(BOTH FROM sol.itemcode), ''), ot.itemcode))
LEFT JOIN majorclassification mc ON mc.majorclassificationcode = i.majorclassificationcode
LEFT JOIN middleclassification mid ON mid.majorclassificationcode = i.majorclassificationcode
  AND mid.middleclassificationcode = i.middleclassificationcode
WHERE COALESCE(ot.releasedate, sol.planneddeliverydate) = {date.Value}
  AND UPPER(TRIM(COALESCE(ot.ordertype, ''))) = 'MO'
  AND ({mfgRoutes.Length} = 0 OR
        TRIM(COALESCE(CASE WHEN CARDINALITY(STRING_TO_ARRAY(parent_ot.productno, '|')) >= 5 THEN SPLIT_PART(parent_ot.productno, '|', 3) ELSE SPLIT_PART(parent_ot.productno, '|', 2) END, '')) = ANY ({mfgRoutes})
      )
  AND ({wcIds.Length} = 0 OR (
        EXISTS (
          SELECT 1 FROM itemworkcentermapping m3
          INNER JOIN workcenter wc ON wc.workcentercode = m3.workcentercode
          WHERE m3.itemcode = TRIM(BOTH FROM COALESCE(NULLIF(TRIM(BOTH FROM sol.itemcode), ''), ot.itemcode))
            AND wc.workcenterid = ANY ({wcIds})
        )
        OR EXISTS (
          SELECT 1 FROM workcenter wc_d
          WHERE (wc_d.workcentercode = TRIM(BOTH FROM ot.workcentercode)
              OR wc_d.workcenterid::text = TRIM(BOTH FROM ot.workcentercode))
            AND wc_d.workcenterid = ANY ({wcIds})
        )
      ))
  AND ({whIds.Length} = 0 OR EXISTS (
        SELECT 1 FROM warehouses wh_f
        WHERE wh_f.warehouseid = ANY ({whIds})
          AND TRIM(COALESCE(wh_f.warehousecode, '')) = TRIM(COALESCE(i.warehousecode, ''))
      ))
  AND ({itemF} = '' OR i.itemcode ILIKE '%' || {itemF} || '%')
  AND ({majorCodes.Length} = 0 OR TRIM(COALESCE(i.majorclassificationcode, '')) = ANY ({majorCodes}))
  AND ({middleCodeFilter} = '' OR TRIM(COALESCE(i.middleclassificationcode, '')) = {middleCodeFilter})
  -- [DEDUP-productno] 同一品目・同一productno（同一実効日付）は最新ordertableidのみ採用（MRP重複対策）
  AND (
    COALESCE(TRIM(ot.productno), '') = ''
    OR ot.ordertableid = (
      SELECT MAX(o2.ordertableid) FROM ordertable o2
      WHERE TRIM(o2.productno) = TRIM(ot.productno)
        AND TRIM(o2.itemcode) = TRIM(ot.itemcode)
        AND COALESCE(o2.releasedate, o2.needdate) IS NOT DISTINCT FROM COALESCE(ot.releasedate, ot.needdate)
    )
  )
GROUP BY
  TO_CHAR(COALESCE(ot.releasedate, sol.planneddeliverydate), 'YYYYMMDD'),
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
LEFT JOIN ordertable parent_ot ON parent_ot.ordertableid = ot.parentordertableid
INNER JOIN item i ON TRIM(BOTH FROM i.itemcode) = TRIM(BOTH FROM COALESCE(NULLIF(TRIM(BOTH FROM sol.itemcode), ''), ot.itemcode))
LEFT JOIN majorclassification mc ON mc.majorclassificationcode = i.majorclassificationcode
LEFT JOIN middleclassification midt ON midt.majorclassificationcode = i.majorclassificationcode
  AND midt.middleclassificationcode = i.middleclassificationcode
WHERE COALESCE(ot.releasedate, sol.planneddeliverydate) = {date.Value}
  AND UPPER(TRIM(COALESCE(ot.ordertype, ''))) = 'MO'
  AND TO_CHAR(COALESCE(ot.releasedate, sol.planneddeliverydate), 'YYYYMMDD') = {key.Delvedt}
  AND COALESCE(mc.majorclassificationcode, '') = {maj}
  AND COALESCE(midt.middleclassificationcode, '') = {mid}
  AND ({mfgRoutes.Length} = 0 OR
        TRIM(COALESCE(CASE WHEN CARDINALITY(STRING_TO_ARRAY(parent_ot.productno, '|')) >= 5 THEN SPLIT_PART(parent_ot.productno, '|', 3) ELSE SPLIT_PART(parent_ot.productno, '|', 2) END, '')) = ANY ({mfgRoutes})
      )
  AND ({wcIds.Length} = 0 OR (
        EXISTS (
          SELECT 1 FROM itemworkcentermapping m3
          INNER JOIN workcenter wc ON wc.workcentercode = m3.workcentercode
          WHERE m3.itemcode = TRIM(BOTH FROM COALESCE(NULLIF(TRIM(BOTH FROM sol.itemcode), ''), ot.itemcode))
            AND wc.workcenterid = ANY ({wcIds})
        )
        OR EXISTS (
          SELECT 1 FROM workcenter wc_d
          WHERE (wc_d.workcentercode = TRIM(BOTH FROM ot.workcentercode)
              OR wc_d.workcenterid::text = TRIM(BOTH FROM ot.workcentercode))
            AND wc_d.workcenterid = ANY ({wcIds})
        )
      ))
  AND ({whIds.Length} = 0 OR EXISTS (
        SELECT 1 FROM warehouses wh_f
        WHERE wh_f.warehouseid = ANY ({whIds})
          AND TRIM(COALESCE(wh_f.warehousecode, '')) = TRIM(COALESCE(i.warehousecode, ''))
      ))
  AND ({itemF} = '' OR i.itemcode ILIKE '%' || {itemF} || '%')
  AND ot.ordertableid IS NOT NULL
  -- [DEDUP-productno] 同一品目・同一productno（同一実効日付）は最新ordertableidのみ採用（MRP重複対策）
  AND (
    COALESCE(TRIM(ot.productno), '') = ''
    OR ot.ordertableid = (
      SELECT MAX(o2.ordertableid) FROM ordertable o2
      WHERE TRIM(o2.productno) = TRIM(ot.productno)
        AND TRIM(o2.itemcode) = TRIM(ot.itemcode)
        AND COALESCE(o2.releasedate, o2.needdate) IS NOT DISTINCT FROM COALESCE(ot.releasedate, ot.needdate)
    )
  )
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
        var bomCache = new Dictionary<(string ParentItemcode, string FacilityCode), List<PreparationBomSqlRow>>();
        var rows = new List<PreparationCsvRow>();

        // 同一製造便・同一品目・同一製番区分のオーダーを集約して数量を合算。
        // 製番区分（productno の有無）をキーに含めるのは、製番品（袋品）とMRP品の合算を防ぐため。
        // MRP品の製造便は親からの継承値であり、製番品の便と一致しても同一オーダー群とは言えない。
        var groups = headers
            .GroupBy(BuildAggregationKey)
            .ToList();

        foreach (var grp in groups)
        {
            var first = grp.First();
            var totalMfgQty = grp.Sum(h => h.MfgQty);
            var mergedOrderNo = string.Join("・", grp.Select(h => h.Ordertableid));
            var asof = first.PlannedDeliveryDate ?? first.NeedDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

            // BOM は受注明細の工場コードで絞り込む（未設定なら MATSUYAMA）
            var facility = BomFacility.Resolve(first.FacilityCode);
            var bomKey = (first.ParentItemcode, facility);
            if (!bomCache.TryGetValue(bomKey, out var boms))
            {
                boms = await FetchBomsForParentAsync(first.ParentItemcode, asof, facility, ct);
                bomCache[bomKey] = boms;
            }

            if (boms.Count == 0)
            {
                rows.Add(new PreparationCsvRow
                {
                    SterilizationTemperatureRange = "",
                    WorkplaceName = first.WorkplaceNames,
                    DeliveryDate = first.NeedDate?.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture) ?? "",
                    Slot = first.SlotDisplay,
                    SmallClassName = first.MinorClassName,
                    OrderNo = mergedOrderNo,
                    ParentItemcode = first.ParentItemcode,
                    ParentItemname = first.ParentItemname,
                    ChildItemcode = "",
                    ChildItemname = "",
                    WarehouseDisplay = "",
                    Quantity = "",
                    Unit = "",
                    ProductionOrder = null
                });
                continue;
            }

            foreach (var b in boms)
            {
                var qty = PreparationBomQuantity.ComputeRequiredQty(totalMfgQty, b.InputQty, b.OutputQty, b.YieldPercent);
                var qtyDisplay = ReportQuantityFormatter.FormatCeilingQuantity(qty);
                rows.Add(new PreparationCsvRow
                {
                    SterilizationTemperatureRange = b.ChildSteriTempRange ?? "",
                    WorkplaceName = first.WorkplaceNames,
                    DeliveryDate = first.NeedDate?.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture) ?? "",
                    Slot = first.SlotDisplay,
                    SmallClassName = first.MinorClassName,
                    OrderNo = mergedOrderNo,
                    ParentItemcode = first.ParentItemcode,
                    ParentItemname = first.ParentItemname,
                    ChildItemcode = b.ChildItemcode,
                    ChildItemname = b.ChildItemname ?? "",
                    WarehouseDisplay = FormatWarehouseDisplay(b.ChildWarehouseCode, b.ChildWarehouseName),
                    Quantity = qtyDisplay,
                    Unit = b.ChildUnitname ?? "",
                    ProductionOrder = b.ProductionOrder
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
        var bomCache = new Dictionary<(string ParentItemcode, string FacilityCode), List<PreparationBomSqlRow>>();
        var lines = new List<PreparationPdfLineModel>();

        // 同一製造便・同一品目・同一製番区分のオーダーを集約して数量を合算。
        // 製番区分（productno の有無）をキーに含めるのは、製番品（袋品）とMRP品の合算を防ぐため。
        // MRP品の製造便は親からの継承値であり、製番品の便と一致しても同一オーダー群とは言えない。
        var groups = headers
            .GroupBy(BuildAggregationKey)
            .ToList();

        foreach (var grp in groups)
        {
            var first = grp.First();
            var totalMfgQty = grp.Sum(h => h.MfgQty);
            var mergedOrderNo = string.Join("・", grp.Select(h => h.Ordertableid));
            var hasProductNo = grp.Key.HasProductNo;
            var asof = first.PlannedDeliveryDate ?? first.NeedDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

            // BOM は受注明細の工場コードで絞り込む（未設定なら MATSUYAMA）
            var facility = BomFacility.Resolve(first.FacilityCode);
            var bomKey = (first.ParentItemcode, facility);
            if (!bomCache.TryGetValue(bomKey, out var boms))
            {
                boms = await FetchBomsForParentAsync(first.ParentItemcode, asof, facility, ct);
                bomCache[bomKey] = boms;
            }

            if (boms.Count == 0)
            {
                lines.Add(new PreparationPdfLineModel
                {
                    MiddleClassificationName = first.MiddleClassName,
                    DateDisplay = first.NeedDate?.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture) ?? "",
                    WorkplaceName = first.WorkplaceNames,
                    WorkplaceCode = first.WorkplaceCode,
                    ManufacturingRouteCode = first.ManufacturingRouteCode,
                    SlotDisplay = first.SlotDisplay,
                    MiddleClassificationCode = first.MiddleClassificationCode,
                    OrderNo = mergedOrderNo,
                    ParentItemcode = first.ParentItemcode,
                    ParentItemname = first.ParentItemname,
                    ChildItemcode = "",
                    ChildItemname = "",
                    Standard = "",
                    TemperatureRange = "",
                    Quantity = "",
                    Unit = "",
                    WarehouseName = "",
                    HasProductNo = hasProductNo,
                    ProductionOrder = null
                });
                continue;
            }

            foreach (var b in boms)
            {
                var qty = PreparationBomQuantity.ComputeRequiredQty(totalMfgQty, b.InputQty, b.OutputQty, b.YieldPercent);
                var qtyDisplay = ReportQuantityFormatter.FormatCeilingQuantity(qty);

                lines.Add(new PreparationPdfLineModel
                {
                    MiddleClassificationName = first.MiddleClassName,
                    DateDisplay = first.NeedDate?.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture) ?? "",
                    WorkplaceName = first.WorkplaceNames,
                    WorkplaceCode = first.WorkplaceCode,
                    ManufacturingRouteCode = first.ManufacturingRouteCode,
                    SlotDisplay = first.SlotDisplay,
                    MiddleClassificationCode = first.MiddleClassificationCode,
                    OrderNo = mergedOrderNo,
                    ParentItemcode = first.ParentItemcode,
                    ParentItemname = first.ParentItemname,
                    ChildItemcode = b.ChildItemcode,
                    ChildItemname = b.ChildItemname ?? "",
                    Standard = b.ChildStd ?? "",
                    TemperatureRange = b.ChildSteriTempRange ?? "",
                    Quantity = qtyDisplay,
                    Unit = b.ChildUnitname ?? "",
                    WarehouseName = b.ChildWarehouseName ?? "",
                    HasProductNo = hasProductNo,
                    ProductionOrder = b.ProductionOrder
                });
            }
        }

        return lines;
    }

    /// <summary>製番区分。<c>ordertable.productno</c> があれば製番品（袋品）、無ければMRP品。</summary>
    internal static bool HasProductNoValue(PreparationLineHeaderRow header)
        => !string.IsNullOrWhiteSpace(header.ProductNo);

    /// <summary>
    /// 数量合算の単位（製造便コード・親品目コード・製番区分）。
    /// 製番区分を含めないと、親から便を継承したMRP品が同一便の製番品と合算される。
    /// </summary>
    internal static (string ManufacturingRouteCode, string ParentItemcode, bool HasProductNo) BuildAggregationKey(
        PreparationLineHeaderRow header)
        => (header.ManufacturingRouteCode, header.ParentItemcode, HasProductNoValue(header));

    private static string FormatWarehouseDisplay(string? code, string? name)
    {
        var c = string.IsNullOrWhiteSpace(code) ? "" : code.Trim();
        var n = string.IsNullOrWhiteSpace(name) ? "" : name.Trim();
        if (c.Length == 0 && n.Length == 0) return "";
        if (c.Length == 0) return n;
        if (n.Length == 0) return c;
        return $"{c}・{n}";
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
        sb.AppendLine("職場名,日付,製造便,分類名,注番,親品目コード,親品目,子品目コード,子品目,倉庫,数量,単位");
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
                .Append(Esc(r.WarehouseDisplay)).Append(',')
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
                  COALESCE(NULLIF(TRIM(ds.slotname), ''), NULLIF(sc.slotcode, ''), '') AS slot_display,
                  COALESCE(
                    NULLIF(TRIM(BOTH FROM wc_ord.workcentername), ''),
                    (SELECT string_agg(DISTINCT wc_map.workcentername, '、' ORDER BY wc_map.workcentername)
                     FROM itemworkcentermapping m2
                     INNER JOIN workcenter wc_map ON wc_map.workcentercode = m2.workcentercode
                     WHERE m2.itemcode = i.itemcode),
                    ''
                  ) AS workplace_names,
                  COALESCE(sol.planneddeliverydate, ot.releasedate) AS planned_delivery,
                  COALESCE(ot.releasedate, sol.planneddeliverydate) AS need_date,
                  COALESCE(
                    NULLIF(TRIM(BOTH FROM wc_ord.workcentercode), ''),
                    (SELECT string_agg(DISTINCT TRIM(BOTH FROM wc_map.workcentercode), '、' ORDER BY TRIM(BOTH FROM wc_map.workcentercode))
                     FROM itemworkcentermapping m2
                     INNER JOIN workcenter wc_map ON wc_map.workcentercode = m2.workcentercode
                     WHERE m2.itemcode = i.itemcode),
                    ''
                  ) AS workplace_code,
                  sc.slotcode AS manufacturing_route_code,
                  COALESCE(mid.middleclassificationcode, '') AS middle_class_code,
                  ot.productno,
                  COALESCE(sol.facilitycode, '') AS facility_code
                FROM ordertable ot
                LEFT JOIN workcenter wc_ord ON (
                     wc_ord.workcentercode = TRIM(BOTH FROM ot.workcentercode)
                  OR wc_ord.workcenterid::text = TRIM(BOTH FROM ot.workcentercode)
                )
                LEFT JOIN salesorderline sol ON sol.salesorderlineid = ot.salesorderlineid
                LEFT JOIN ordertable parent_ot ON parent_ot.ordertableid = ot.parentordertableid
                LEFT JOIN ordertable gp_ot ON gp_ot.ordertableid = parent_ot.parentordertableid
                INNER JOIN item i ON TRIM(BOTH FROM i.itemcode) = TRIM(BOTH FROM COALESCE(NULLIF(TRIM(BOTH FROM sol.itemcode), ''), ot.itemcode))
                LEFT JOIN minorclassification mn ON mn.majorclassificationcode = i.majorclassificationcode
                  AND mn.middleclassificationcode = i.middleclassificationcode
                  AND mn.minorclassificationcode = i.minorclassificationcode
                LEFT JOIN middleclassification mid ON mid.majorclassificationcode = i.majorclassificationcode
                  AND mid.middleclassificationcode = i.middleclassificationcode
                CROSS JOIN LATERAL (
                  SELECT COALESCE(
                    -- 自オーダーが製番品ならその便
                    NULLIF(TRIM(COALESCE(CASE WHEN CARDINALITY(STRING_TO_ARRAY(ot.productno, '|')) >= 5 THEN SPLIT_PART(ot.productno, '|', 3) ELSE SPLIT_PART(ot.productno, '|', 2) END, '')), ''),
                    -- [SLOT-CHAIN] 親を遡るのは親自身が製番品（productno非空）のときのみ。
                    -- 自オーダーにproductnoが無い＝MRP品で、親もproductno空なら製造便なし（''）として扱う。
                    CASE WHEN COALESCE(TRIM(parent_ot.productno), '') <> ''
                         THEN NULLIF(TRIM(COALESCE(CASE WHEN CARDINALITY(STRING_TO_ARRAY(parent_ot.productno, '|')) >= 5 THEN SPLIT_PART(parent_ot.productno, '|', 3) ELSE SPLIT_PART(parent_ot.productno, '|', 2) END, '')), '')
                    END,
                    CASE WHEN COALESCE(TRIM(parent_ot.productno), '') <> '' AND COALESCE(TRIM(gp_ot.productno), '') <> ''
                         THEN NULLIF(TRIM(COALESCE(CASE WHEN CARDINALITY(STRING_TO_ARRAY(gp_ot.productno, '|')) >= 5 THEN SPLIT_PART(gp_ot.productno, '|', 3) ELSE SPLIT_PART(gp_ot.productno, '|', 2) END, '')), '')
                    END,
                    ''
                  ) AS slotcode
                ) sc
                LEFT JOIN deliveryslot ds ON ds.slotcode = sc.slotcode
                WHERE ot.ordertableid = ANY(@ids)
                  -- [DEDUP-productno] 同一品目・同一productno（同一実効日付）は最新ordertableidのみ採用（MRP重複対策）
                  AND (
                    COALESCE(TRIM(ot.productno), '') = ''
                    OR ot.ordertableid = (
                      SELECT MAX(o2.ordertableid) FROM ordertable o2
                      WHERE TRIM(o2.productno) = TRIM(ot.productno)
                        AND TRIM(o2.itemcode) = TRIM(ot.itemcode)
                        AND COALESCE(o2.releasedate, o2.needdate) IS NOT DISTINCT FROM COALESCE(ot.releasedate, ot.needdate)
                    )
                  )
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
                    ProductNo = reader.IsDBNull(15) ? null : reader.GetString(15),
                    FacilityCode = reader.IsDBNull(16) ? null : reader.GetString(16)
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

    private async Task<List<PreparationBomSqlRow>> FetchBomsForParentAsync(string parentItemcode, DateOnly asOf, string facilityCode, CancellationToken ct)
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
                  COALESCE(wh_child.warehousecode, '') AS child_warehouse_code,
                  COALESCE(wh_child.warehousename, '') AS child_warehouse_name,
                  b.productionorder
                FROM bom b
                LEFT JOIN item ci ON TRIM(ci.itemcode) = TRIM(b.childitemcode)
                LEFT JOIN warehouses wh_child ON wh_child.warehousecode = ci.warehousecode
                LEFT JOIN unit u ON u.unitcode = ci.unitcode0
                LEFT JOIN itemadditionalinformation ia ON TRIM(ia.itemcode) = TRIM(b.childitemcode)
                WHERE b.parentitemcode = @p
                  AND b.childitemcode IS NOT NULL
                  AND TRIM(BOTH FROM COALESCE(b.facilitycode, '')) = @facility
                  AND (b.startdate IS NULL OR b.startdate <= @asof)
                  AND (b.enddate IS NULL OR b.enddate >= @asof)
                ORDER BY b.productionorder NULLS LAST, b.childitemcode
                """, conn);
            cmd.Parameters.AddWithValue("p", parentItemcode);
            cmd.Parameters.AddWithValue("asof", asOf);
            cmd.Parameters.AddWithValue("facility", facilityCode);
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
                    ChildWarehouseCode = reader.GetString(8),
                    ChildWarehouseName = reader.GetString(9),
                    ProductionOrder = reader.IsDBNull(10) ? null : reader.GetDecimal(10)
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
    public string WarehouseDisplay { get; set; } = "";
    public string Quantity { get; set; } = "";
    public string Unit { get; set; } = "";

    /// <summary>並び順用: レシピ（BOM）の並び順（<c>bom.productionorder</c>）。NULL は末尾。CSV の列には出力しない。</summary>
    public decimal? ProductionOrder { get; set; }
}

public sealed class PreparationPdfLineModel
{
    public string MiddleClassificationName { get; set; } = "";
    public string DateDisplay { get; set; } = "";
    public string WorkplaceName { get; set; } = "";
    public string WorkplaceCode { get; set; } = "";
    public string ManufacturingRouteCode { get; set; } = "";
    /// <summary>製造便の表示名（<c>deliveryslot.slotname</c>、無ければ slotcode）。</summary>
    public string SlotDisplay { get; set; } = "";
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
    /// <summary>ordertable.productno が存在するか（true=袋品, false=その他）。改頁判定・並び順のみに使用し、帳票には表示しない。</summary>
    public bool HasProductNo { get; set; }

    /// <summary>並び順用: レシピ（BOM）の並び順（<c>bom.productionorder</c>）。NULL は末尾。</summary>
    public decimal? ProductionOrder { get; set; }
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

    /// <summary>受注明細の工場コード（<c>salesorderline.facilitycode</c>）。BOM 取得の絞り込みに使用。未設定なら MATSUYAMA。</summary>
    public string? FacilityCode { get; set; }
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
    /// <summary>子品目の保管倉庫コード（<c>item.warehousecode</c> → <c>warehouses.warehousecode</c>）。</summary>
    public string? ChildWarehouseCode { get; set; }
    /// <summary>子品目の保管倉庫名（<c>item.warehousecode</c> → <c>warehouses.warehousename</c>）。</summary>
    public string? ChildWarehouseName { get; set; }

    /// <summary>レシピ（BOM）の並び順（<c>bom.productionorder</c>）。NULL は末尾。</summary>
    public decimal? ProductionOrder { get; set; }
}
