using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using BaggingInstructions.Api.Core;
using BaggingInstructions.Api.DTOs;
using BaggingInstructions.Api.QueryResults;

namespace BaggingInstructions.Api.Services;

/// <summary>
/// カット前準備書・現品ラベル画面のデータ取得サービス。
/// 子品目コードの先頭2桁が50または51の品目のみ対象。連続する同一子品目は先頭のみ出力。
/// </summary>
public class CutPreparationService
{
    private readonly AppDbContext _db;

    public CutPreparationService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>製造日・製造便・品目コード・作業区で受注明細を集約（日付×製造便ごとの件数）。</summary>
    public async Task<List<CutPreparationGroupDto>> SearchGroupsAsync(
        string delvedt,
        IReadOnlyList<string> manufacturingRouteCodes,
        string? itemcd,
        IReadOnlyList<long> workcenterIds,
        CancellationToken ct = default)
    {
        var date = ParseYyyymmdd(delvedt);
        if (!date.HasValue)
            throw new ArgumentException("製造日はYYYYMMDD形式（8桁）で指定してください。", nameof(delvedt));

        var mfgRoutes = (manufacturingRouteCodes ?? Array.Empty<string>())
            .Select(s => (s ?? "").Trim())
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var wcIds = (workcenterIds ?? Array.Empty<long>()).Where(id => id > 0).Distinct().ToArray();
        var itemF = itemcd?.Trim() ?? "";

        var rows = await _db.Database
            .SqlQuery<CutPreparationGroupSqlRow>($@"
WITH mfg AS (
  SELECT
    ot.ordertableid,
    COALESCE(ot.needdate, sol.planneddeliverydate) AS needdate_combined,
    TRIM(BOTH FROM COALESCE(NULLIF(TRIM(BOTH FROM sol.itemcode), ''), ot.itemcode)) AS item_code,
    COALESCE(
      NULLIF(TRIM(COALESCE(CASE WHEN CARDINALITY(STRING_TO_ARRAY(ot.productno, '|')) >= 5 THEN SPLIT_PART(ot.productno, '|', 3) ELSE SPLIT_PART(ot.productno, '|', 2) END, '')), ''),
      NULLIF(TRIM(COALESCE(CASE WHEN CARDINALITY(STRING_TO_ARRAY(p.productno, '|')) >= 5 THEN SPLIT_PART(p.productno, '|', 3) ELSE SPLIT_PART(p.productno, '|', 2) END, '')), ''),
      NULLIF(TRIM(COALESCE(CASE WHEN CARDINALITY(STRING_TO_ARRAY(gp.productno, '|')) >= 5 THEN SPLIT_PART(gp.productno, '|', 3) ELSE SPLIT_PART(gp.productno, '|', 2) END, '')), ''),
      ''
    ) AS route_code
  FROM ordertable ot
  LEFT JOIN salesorderline sol ON sol.salesorderlineid = ot.salesorderlineid
  LEFT JOIN ordertable p  ON p.ordertableid  = ot.parentordertableid
  LEFT JOIN ordertable gp ON gp.ordertableid = p.parentordertableid
)
SELECT
  TO_CHAR(m.needdate_combined, 'YYYYMMDD') AS ""Delvedt"",
  m.route_code AS ""MfgRouteCode"",
  COALESCE(NULLIF(TRIM(COALESCE(ds.slotname, '')), ''), m.route_code) AS ""MfgRouteName"",
  COUNT(DISTINCT m.ordertableid)::int AS ""LineCount""
FROM mfg m
LEFT JOIN deliveryslot ds ON ds.slotcode = m.route_code
WHERE m.needdate_combined = {date.Value}
  AND ({mfgRoutes.Length} = 0 OR m.route_code = ANY({mfgRoutes}))
  AND ({wcIds.Length} = 0 OR EXISTS (
        SELECT 1 FROM itemworkcentermapping m3
        INNER JOIN workcenter wc ON wc.workcentercode = m3.workcentercode
        WHERE m3.itemcode = m.item_code
          AND wc.workcenterid = ANY({wcIds})
      ))
  AND ({itemF} = '' OR m.item_code ILIKE '%' || {itemF} || '%')
GROUP BY
  TO_CHAR(m.needdate_combined, 'YYYYMMDD'),
  m.route_code,
  ds.slotname
ORDER BY ""Delvedt"", ""MfgRouteCode""
")
            .ToListAsync(ct);

        return rows.Select(r => new CutPreparationGroupDto
        {
            Delvedt = r.Delvedt,
            ManufacturingRouteCode = r.MfgRouteCode,
            ManufacturingRouteName = string.IsNullOrWhiteSpace(r.MfgRouteName) ? r.MfgRouteCode : r.MfgRouteName,
            LineCount = r.LineCount,
            Key = new CutPreparationGroupKeyDto
            {
                Delvedt = r.Delvedt,
                ManufacturingRouteCode = r.MfgRouteCode
            }
        }).ToList();
    }

    /// <summary>選択グループのordertableIDを解決する。</summary>
    public async Task<IReadOnlyList<long>> ResolveLineIdsAsync(
        CutPreparationFilterRequestDto filter,
        IReadOnlyList<CutPreparationGroupKeyDto> groupKeys,
        CancellationToken ct = default)
    {
        if (groupKeys == null || groupKeys.Count == 0)
            return Array.Empty<long>();

        var date = ParseYyyymmdd(filter.Delvedt ?? "");
        if (!date.HasValue)
            throw new ArgumentException("製造日はYYYYMMDD形式（8桁）で指定してください。", nameof(filter.Delvedt));

        var mfgRoutes = (filter.ManufacturingRouteCodes ?? new List<string>())
            .Select(s => (s ?? "").Trim())
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var wcIds = (filter.WorkcenterIds ?? new List<long>()).Where(id => id > 0).Distinct().ToArray();
        var itemF = filter.Itemcd?.Trim() ?? "";

        var all = new HashSet<long>();
        foreach (var key in groupKeys)
        {
            var routeCode = key.ManufacturingRouteCode ?? "";
            var ids = await _db.Database
                .SqlQuery<CutPreparationLineIdSqlRow>($@"
SELECT DISTINCT ot.ordertableid AS ""Ordertableid""
FROM ordertable ot
LEFT JOIN salesorderline sol ON sol.salesorderlineid = ot.salesorderlineid
LEFT JOIN ordertable p  ON p.ordertableid  = ot.parentordertableid
LEFT JOIN ordertable gp ON gp.ordertableid = p.parentordertableid
WHERE COALESCE(ot.needdate, sol.planneddeliverydate) = {date.Value}
  AND TO_CHAR(COALESCE(ot.needdate, sol.planneddeliverydate), 'YYYYMMDD') = {key.Delvedt}
  AND COALESCE(
        NULLIF(TRIM(COALESCE(CASE WHEN CARDINALITY(STRING_TO_ARRAY(ot.productno, '|')) >= 5 THEN SPLIT_PART(ot.productno, '|', 3) ELSE SPLIT_PART(ot.productno, '|', 2) END, '')), ''),
        NULLIF(TRIM(COALESCE(CASE WHEN CARDINALITY(STRING_TO_ARRAY(p.productno, '|')) >= 5 THEN SPLIT_PART(p.productno, '|', 3) ELSE SPLIT_PART(p.productno, '|', 2) END, '')), ''),
        NULLIF(TRIM(COALESCE(CASE WHEN CARDINALITY(STRING_TO_ARRAY(gp.productno, '|')) >= 5 THEN SPLIT_PART(gp.productno, '|', 3) ELSE SPLIT_PART(gp.productno, '|', 2) END, '')), ''),
        ''
      ) = {routeCode}
  AND ({wcIds.Length} = 0 OR EXISTS (
        SELECT 1 FROM itemworkcentermapping m3
        INNER JOIN workcenter wc ON wc.workcentercode = m3.workcentercode
        WHERE m3.itemcode = TRIM(BOTH FROM COALESCE(NULLIF(TRIM(BOTH FROM sol.itemcode), ''), ot.itemcode))
          AND wc.workcenterid = ANY({wcIds})
      ))
  AND ({itemF} = '' OR TRIM(BOTH FROM COALESCE(NULLIF(TRIM(BOTH FROM sol.itemcode), ''), ot.itemcode)) ILIKE '%' || {itemF} || '%')
  AND ot.ordertableid IS NOT NULL
")
                .ToListAsync(ct);
            foreach (var row in ids)
                all.Add(row.Ordertableid);
        }

        return all.OrderBy(x => x).ToList();
    }

    /// <summary>
    /// PDF用明細モデルを構築する。子品目コードの先頭2桁が50/51のもののみ対象。
    /// ソート後、連続する同一子品目コードは先頭のみ出力。
    /// </summary>
    public async Task<List<CutPreparationPdfLineModel>> BuildPdfLineModelsAsync(
        IReadOnlyList<long> lineIds,
        CancellationToken ct = default)
    {
        if (lineIds.Count == 0)
            return new List<CutPreparationPdfLineModel>();

        var headers = await FetchLineHeadersAsync(lineIds, ct);
        var bomCache = new Dictionary<string, List<PreparationBomSqlRow>>(StringComparer.Ordinal);

        var lines = new List<CutPreparationPdfLineModel>();
        foreach (var h in headers)
        {
            var asof = h.PlannedDeliveryDate ?? h.NeedDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var bomKey = h.ParentItemcode;
            if (!bomCache.TryGetValue(bomKey, out var boms))
            {
                boms = await FetchBomsForParentAsync(bomKey, asof, ct);
                bomCache[bomKey] = boms;
            }

            // 子品目コードが50/51始まりのものだけ抽出
            var filtered = boms.Where(b => IsCutTargetItem(b.ChildItemcode)).ToList();

            if (filtered.Count == 0)
                continue;

            foreach (var b in filtered)
            {
                var qty = PreparationBomQuantity.ComputeRequiredQty(h.MfgQty, b.InputQty, b.OutputQty, b.YieldPercent);
                lines.Add(new CutPreparationPdfLineModel
                {
                    DateDisplay = h.NeedDate?.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture) ?? "",
                    ManufacturingRoute = h.MfgRoute,
                    MiddleClassName = h.MiddleClassName,
                    FinalProductName = h.FinalProductName,
                    ParentItemcode = h.ParentItemcode,
                    ParentItemname = h.ParentItemname,
                    ChildItemcode = b.ChildItemcode,
                    ChildItemname = b.ChildItemname ?? "",
                    Quantity = ReportQuantityFormatter.FormatCeilingQuantity(qty),
                    Unit = b.ChildUnitname ?? "",
                    WarehouseName = b.ChildWarehouseName ?? "",
                    OrderNo = h.Ordertableid.ToString(CultureInfo.InvariantCulture)
                });
            }
        }

        // ソート: 日付 → 製造便 → 親品目コード → 子品目コード
        var sorted = lines
            .OrderBy(l => l.DateDisplay, StringComparer.Ordinal)
            .ThenBy(l => l.ManufacturingRoute, StringComparer.Ordinal)
            .ThenBy(l => l.ParentItemcode, StringComparer.Ordinal)
            .ThenBy(l => l.ChildItemcode, StringComparer.Ordinal)
            .ToList();

        // 連続する同一子品目コードを除去
        return DeduplicateConsecutive(sorted);
    }

    private static bool IsCutTargetItem(string? itemcode)
    {
        if (string.IsNullOrEmpty(itemcode)) return false;
        var code = itemcode.Trim();
        return code.Length >= 2 && (code.StartsWith("50", StringComparison.Ordinal) || code.StartsWith("51", StringComparison.Ordinal));
    }

    private static List<CutPreparationPdfLineModel> DeduplicateConsecutive(List<CutPreparationPdfLineModel> sorted)
    {
        var result = new List<CutPreparationPdfLineModel>(sorted.Count);
        string? prevChildCode = null;
        foreach (var line in sorted)
        {
            if (line.ChildItemcode != prevChildCode)
            {
                result.Add(line);
                prevChildCode = line.ChildItemcode;
            }
        }
        return result;
    }

    private static DateOnly? ReadDateNullable(NpgsqlDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal)) return null;
        var o = reader.GetValue(ordinal);
        if (o is DateOnly d) return d;
        if (o is DateTime dt) return DateOnly.FromDateTime(dt);
        return null;
    }

    private async Task<List<CutPreparationLineHeaderRow>> FetchLineHeadersAsync(
        IReadOnlyList<long> lineIds, CancellationToken ct)
    {
        var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
        var shouldClose = conn.State != ConnectionState.Open;
        if (shouldClose) await conn.OpenAsync(ct);
        try
        {
            await using var cmd = new NpgsqlCommand(
                """
                WITH base AS (
                  SELECT
                    COALESCE(ot.ordertableid, 0) AS ordertableid,
                    COALESCE(ot.qty, sol.quantity) AS mfg_qty,
                    TRIM(BOTH FROM COALESCE(NULLIF(TRIM(BOTH FROM sol.itemcode), ''), ot.itemcode)) AS parent_itemcode,
                    COALESCE(parent_i.itemname, '') AS parent_itemname,
                    TRIM(COALESCE(ot.itemcode, '')) AS ot_itemcode,
                    CASE
                      WHEN TRIM(COALESCE(NULLIF(TRIM(BOTH FROM sol.itemcode), ''), '')) <> ''
                           AND TRIM(COALESCE(NULLIF(TRIM(BOTH FROM sol.itemcode), ''), '')) <> TRIM(COALESCE(ot.itemcode, ''))
                      THEN COALESCE(sol_i.itemname, '')
                      ELSE ''
                    END AS final_product_name,
                    COALESCE(mid.middleclassificationname, '') AS middle_class_name,
                    COALESCE(
                      NULLIF(TRIM(COALESCE(CASE WHEN CARDINALITY(STRING_TO_ARRAY(ot.productno, '|')) >= 5 THEN SPLIT_PART(ot.productno, '|', 3) ELSE SPLIT_PART(ot.productno, '|', 2) END, '')), ''),
                      NULLIF(TRIM(COALESCE(CASE WHEN CARDINALITY(STRING_TO_ARRAY(p.productno, '|')) >= 5 THEN SPLIT_PART(p.productno, '|', 3) ELSE SPLIT_PART(p.productno, '|', 2) END, '')), ''),
                      NULLIF(TRIM(COALESCE(CASE WHEN CARDINALITY(STRING_TO_ARRAY(gp.productno, '|')) >= 5 THEN SPLIT_PART(gp.productno, '|', 3) ELSE SPLIT_PART(gp.productno, '|', 2) END, '')), ''),
                      ''
                    ) AS route_code,
                    COALESCE(sol.planneddeliverydate, ot.releasedate) AS planned_delivery,
                    COALESCE(ot.needdate, sol.planneddeliverydate) AS need_date
                  FROM ordertable ot
                  LEFT JOIN salesorderline sol ON sol.salesorderlineid = ot.salesorderlineid
                  LEFT JOIN ordertable p  ON p.ordertableid  = ot.parentordertableid
                  LEFT JOIN ordertable gp ON gp.ordertableid = p.parentordertableid
                  INNER JOIN item parent_i ON TRIM(BOTH FROM parent_i.itemcode) = TRIM(BOTH FROM COALESCE(NULLIF(TRIM(BOTH FROM sol.itemcode), ''), ot.itemcode))
                  LEFT JOIN item sol_i ON TRIM(sol_i.itemcode) = TRIM(COALESCE(NULLIF(TRIM(sol.itemcode), ''), ''))
                  LEFT JOIN middleclassification mid ON mid.majorclassificationcode = parent_i.majorclassficationcode
                    AND mid.middleclassificationcode = parent_i.middleclassficationcode
                  WHERE ot.ordertableid = ANY(@ids)
                )
                SELECT
                  b.ordertableid,
                  b.mfg_qty,
                  b.parent_itemcode,
                  b.parent_itemname,
                  b.ot_itemcode,
                  b.final_product_name,
                  b.middle_class_name,
                  COALESCE(NULLIF(TRIM(COALESCE(ds.slotname, '')), ''), b.route_code) AS mfg_route,
                  b.planned_delivery,
                  b.need_date
                FROM base b
                LEFT JOIN deliveryslot ds ON ds.slotcode = b.route_code
                ORDER BY b.ordertableid
                """, conn);
            cmd.Parameters.Add(new NpgsqlParameter("ids", NpgsqlDbType.Bigint | NpgsqlDbType.Array)
            {
                Value = lineIds.ToArray()
            });
            var list = new List<CutPreparationLineHeaderRow>();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                list.Add(new CutPreparationLineHeaderRow
                {
                    Ordertableid = reader.GetInt64(0),
                    MfgQty = reader.GetDecimal(1),
                    ParentItemcode = reader.GetString(2),
                    ParentItemname = reader.GetString(3),
                    OtItemcode = reader.GetString(4),
                    FinalProductName = reader.GetString(5),
                    MiddleClassName = reader.GetString(6),
                    MfgRoute = reader.GetString(7),
                    PlannedDeliveryDate = ReadDateNullable(reader, 8),
                    NeedDate = ReadDateNullable(reader, 9)
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

    private async Task<List<PreparationBomSqlRow>> FetchBomsForParentAsync(
        string parentItemcode, DateOnly asOf, CancellationToken ct)
    {
        var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
        var shouldClose = conn.State != ConnectionState.Open;
        if (shouldClose) await conn.OpenAsync(ct);
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
                  COALESCE(wh_child.warehousename, wh_child.warehousecode, '') AS child_warehouse_name
                FROM bom b
                LEFT JOIN item ci ON TRIM(ci.itemcode) = TRIM(b.childitemcode)
                LEFT JOIN warehouses wh_child ON wh_child.warehousecode = ci.warehousecode
                LEFT JOIN unit u ON u.unitcode = ci.unitcode0
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
                    ChildWarehouseName = reader.GetString(6)
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

public sealed class CutPreparationPdfLineModel
{
    public string DateDisplay { get; set; } = "";
    public string ManufacturingRoute { get; set; } = "";
    public string MiddleClassName { get; set; } = "";
    /// <summary>最終完成品名称（salesorderline品目がordertable品目と異なる場合）。</summary>
    public string FinalProductName { get; set; } = "";
    public string ParentItemcode { get; set; } = "";
    public string ParentItemname { get; set; } = "";
    public string ChildItemcode { get; set; } = "";
    public string ChildItemname { get; set; } = "";
    public string Quantity { get; set; } = "";
    public string Unit { get; set; } = "";
    public string WarehouseName { get; set; } = "";
    public string OrderNo { get; set; } = "";
}

internal sealed class CutPreparationLineHeaderRow
{
    public long Ordertableid { get; set; }
    public decimal MfgQty { get; set; }
    public string ParentItemcode { get; set; } = "";
    public string ParentItemname { get; set; } = "";
    public string OtItemcode { get; set; } = "";
    public string FinalProductName { get; set; } = "";
    public string MiddleClassName { get; set; } = "";
    public string MfgRoute { get; set; } = "";
    public DateOnly? PlannedDeliveryDate { get; set; }
    public DateOnly? NeedDate { get; set; }
}
