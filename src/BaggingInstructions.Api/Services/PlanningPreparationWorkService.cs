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

/// <summary>
/// 作業前準備書（計画オーダー）。<c>planningorder</c> を基に、親品目＝計画オーダーの品目、子品目＝BOM で展開する。
/// 最終製品は BOM を親方向に遡り、同じ productno の計画オーダーを持つ最上位の品目（複数あれば「、」区切り）。
/// 子品目の数量は単位0と購買単位（<c>item.purchaseorderunitcode</c>）換算の両方を出力する。
/// </summary>
public class PlanningPreparationWorkService
{
    private readonly AppDbContext _db;
    private readonly PreparationWorkService _preparationWorkService;

    public PlanningPreparationWorkService(AppDbContext db, PreparationWorkService preparationWorkService)
    {
        _db = db;
        _preparationWorkService = preparationWorkService;
    }

    /// <summary>
    /// 計画オーダーの検索条件（納期・製造便・作業区・倉庫・品目・大分類・中分類）。
    /// 納期は <c>planningorder.releasedate</c>（NULL なら needdate）、製造便は <c>planningorder.slotcode</c>。
    /// </summary>
    /// <summary>
    /// [DEDUP-mrprunat] MRP の再実行で残った重複オーダーを除外する条件（<c>AND (...)</c> の形）。
    /// 同じグループ（<see cref="SameDedupGroupSql"/>）に注番（<c>ordertable</c>）が紐づくオーダーがあれば、紐づくものだけを採用する。
    /// 無ければ mrprunat が最新のものだけを採用し、mrprunat が NULL（MPS由来・手動登録）は常に採用する。
    /// </summary>
    private static string DedupConditionSql(string alias) => $"""
        AND (
          EXISTS (SELECT 1 FROM ordertable ot_d WHERE ot_d.planningorderid = {alias}.planningorderid)
          OR (
            NOT EXISTS (
              SELECT 1 FROM planningorder p2
              INNER JOIN ordertable ot2 ON ot2.planningorderid = p2.planningorderid
              WHERE {SameDedupGroupSql(alias)}
            )
            AND (
              {alias}.mrprunat IS NULL
              OR NOT EXISTS (
                SELECT 1 FROM planningorder p2
                WHERE {SameDedupGroupSql(alias)}
                  AND p2.mrprunat > {alias}.mrprunat
              )
            )
          )
        )
        """;

    /// <summary>
    /// 重複判定のグループ（<c>p2</c> と <paramref name="alias"/> が同じグループか）。
    /// productno あり: 同一品目・同一productno（日付をまたいで判定）。productno なし: 同一品目・同一工場・同一日付。
    /// </summary>
    private static string SameDedupGroupSql(string alias) => $"""
        p2.itemcode = {alias}.itemcode
                AND COALESCE(TRIM(p2.productno), '') = COALESCE(TRIM({alias}.productno), '')
                AND (
                  COALESCE(TRIM({alias}.productno), '') <> ''
                  OR (COALESCE(TRIM(p2.facilitycode), '') = COALESCE(TRIM({alias}.facilitycode), '')
                      AND COALESCE(p2.releasedate, p2.needdate) IS NOT DISTINCT FROM COALESCE({alias}.releasedate, {alias}.needdate))
                )
        """;

    private static readonly string FilterFromWhereSql = $"""
        FROM planningorder po
        INNER JOIN item i ON TRIM(BOTH FROM i.itemcode) = TRIM(BOTH FROM po.itemcode)
        LEFT JOIN majorclassification mc ON mc.majorclassificationcode = i.majorclassificationcode
        LEFT JOIN middleclassification mid ON mid.majorclassificationcode = i.majorclassificationcode
          AND mid.middleclassificationcode = i.middleclassificationcode
        WHERE COALESCE(po.releasedate, po.needdate) = @date
          AND UPPER(TRIM(COALESCE(po.ordertype, ''))) = 'MO'
          AND (CARDINALITY(@routes) = 0 OR TRIM(COALESCE(po.slotcode, '')) = ANY (@routes))
          AND (CARDINALITY(@wcIds) = 0 OR EXISTS (
                SELECT 1 FROM itemworkcentermapping m3
                INNER JOIN workcenter wc ON wc.workcentercode = m3.workcentercode
                WHERE m3.itemcode = TRIM(BOTH FROM po.itemcode)
                  AND wc.workcenterid = ANY (@wcIds)
              ))
          AND (CARDINALITY(@whIds) = 0 OR EXISTS (
                SELECT 1 FROM warehouses wh_f
                WHERE wh_f.warehouseid = ANY (@whIds)
                  AND TRIM(COALESCE(wh_f.warehousecode, '')) = TRIM(COALESCE(i.warehousecode, ''))
              ))
          AND (@itemF = '' OR i.itemcode ILIKE '%' || @itemF || '%')
          AND (CARDINALITY(@majorCodes) = 0 OR TRIM(COALESCE(i.majorclassificationcode, '')) = ANY (@majorCodes))
          AND (@middleCode = '' OR TRIM(COALESCE(i.middleclassificationcode, '')) = @middleCode)
          {DedupConditionSql("po")}
        """;

    /// <summary>指定納期の計画オーダーに現れる製造便（<c>planningorder.slotcode</c>）の一覧。コード順。</summary>
    public async Task<List<PreparationWorkManufacturingRouteOptionDto>> ListManufacturingRoutesForNeedDateAsync(
        string delvedt,
        CancellationToken ct = default)
    {
        var date = PreparationWorkService.ParseYyyymmdd(delvedt);
        if (!date.HasValue)
            throw new ArgumentException("納期はYYYYMMDD形式（8桁）で指定してください。", nameof(delvedt));

        var rows = await _db.Database
            .SqlQueryRaw<PreparationWorkManufacturingRouteSqlRow>($"""
                SELECT
                  TRIM(po.slotcode) AS "Code",
                  COALESCE(NULLIF(TRIM(ds.slotname), ''), TRIM(po.slotcode)) AS "Name"
                FROM planningorder po
                LEFT JOIN deliveryslot ds ON ds.slotcode = po.slotcode
                WHERE COALESCE(po.releasedate, po.needdate) = @date
                  AND UPPER(TRIM(COALESCE(po.ordertype, ''))) = 'MO'
                  AND COALESCE(TRIM(po.slotcode), '') <> ''
                  {DedupConditionSql("po")}
                GROUP BY TRIM(po.slotcode), TRIM(ds.slotname)
                ORDER BY 1
                """, new NpgsqlParameter("date", NpgsqlDbType.Date) { Value = date.Value })
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
        var filter = await BuildFilterAsync(delvedt, manufacturingRouteCodes, itemcd, majorClassificationIds,
            middleClassificationId, workcenterIds, warehouseIds, ct);

        var rows = await _db.Database
            .SqlQueryRaw<PreparationWorkGroupSqlRow>($"""
                SELECT
                  TO_CHAR(COALESCE(po.releasedate, po.needdate), 'YYYYMMDD') AS "Delvedt",
                  COALESCE(mc.majorclassificationcode, '') AS "MajorCode",
                  COALESCE(mc.majorclassificationname, '') AS "MajorName",
                  COALESCE(mid.middleclassificationcode, '') AS "MiddleCode",
                  COALESCE(mid.middleclassificationname, '') AS "MiddleName",
                  COUNT(*)::int AS "LineCount"
                {FilterFromWhereSql}
                GROUP BY
                  TO_CHAR(COALESCE(po.releasedate, po.needdate), 'YYYYMMDD'),
                  mc.majorclassificationcode,
                  mc.majorclassificationname,
                  mid.middleclassificationcode,
                  mid.middleclassificationname
                ORDER BY "MajorCode", "MiddleCode"
                """, filter.ToParameters())
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

    /// <summary>選択グループ（日付×大分類×中分類）に該当する <c>planningorderid</c> を返す。</summary>
    public async Task<IReadOnlyList<long>> ResolvePlanningOrderIdsAsync(
        PreparationWorkFilterRequestDto filterRequest,
        IReadOnlyList<PreparationWorkGroupKeyDto> groupKeys,
        CancellationToken ct = default)
    {
        if (groupKeys == null || groupKeys.Count == 0)
            return Array.Empty<long>();

        var filter = await BuildFilterAsync(
            filterRequest.Delvedt ?? "",
            filterRequest.ManufacturingRouteCodes ?? new List<string>(),
            filterRequest.Itemcd,
            null,
            null,
            filterRequest.WorkcenterIds ?? new List<long>(),
            filterRequest.WarehouseIds ?? new List<long>(),
            ct);

        var all = new HashSet<long>();
        foreach (var key in groupKeys)
        {
            var parameters = filter.ToParameters().ToList();
            parameters.Add(new NpgsqlParameter("keyDelvedt", NpgsqlDbType.Text) { Value = key.Delvedt ?? "" });
            parameters.Add(new NpgsqlParameter("keyMajor", NpgsqlDbType.Text) { Value = key.MajorClassificationCode ?? "" });
            parameters.Add(new NpgsqlParameter("keyMiddle", NpgsqlDbType.Text) { Value = key.MiddleClassificationCode ?? "" });

            var ids = await _db.Database
                .SqlQueryRaw<PlanningPreparationWorkIdSqlRow>($"""
                    SELECT po.planningorderid AS "Planningorderid"
                    {FilterFromWhereSql}
                      AND TO_CHAR(COALESCE(po.releasedate, po.needdate), 'YYYYMMDD') = @keyDelvedt
                      AND COALESCE(mc.majorclassificationcode, '') = @keyMajor
                      AND COALESCE(mid.middleclassificationcode, '') = @keyMiddle
                    """, parameters.ToArray())
                .ToListAsync(ct);
            foreach (var row in ids)
                all.Add(row.Planningorderid);
        }

        return all.OrderBy(x => x).ToList();
    }

    public async Task<List<PlanningPreparationCsvRow>> BuildCsvRowsAsync(IReadOnlyList<long> planningOrderIds, CancellationToken ct = default)
    {
        if (planningOrderIds.Count == 0)
            return new List<PlanningPreparationCsvRow>();

        var headers = await FetchPlanningOrderHeadersAsync(planningOrderIds, ct);
        var bomCache = new Dictionary<(string ParentItemcode, string FacilityCode), List<PreparationBomSqlRow>>();
        var finalProductCache = new Dictionary<(string ParentItemcode, string ProductNo, string FacilityCode), PlanningFinalProductInfo>();

        // 計画オーダーごとに最終製品（productno で特定）を求める
        var resolved = new List<(PlanningOrderHeaderRow Header, string Facility, PlanningFinalProductInfo Final)>();
        foreach (var h in headers)
        {
            // BOM は計画オーダーの工場コードで絞り込む（未設定なら受注明細の工場、それも無ければ MATSUYAMA）
            var facility = BomFacility.Resolve(h.FacilityCode);
            var productNo = h.ProductNo?.Trim() ?? "";
            var key = (h.ParentItemcode, productNo, facility);
            if (!finalProductCache.TryGetValue(key, out var final))
            {
                final = await ResolveFinalProductAsync(h.ParentItemcode, productNo, AsOf(h), facility, ct);
                finalProductCache[key] = final;
            }
            resolved.Add((h, facility, final));
        }

        // 表示列（最終製品・最終製品の製造日・親品目）が同じ計画オーダーを集約して数量を合算する。
        // 既存の作業前準備書と同様、製番品とMRP品は合算しないよう製番区分もキーに含める。
        var groups = resolved
            .GroupBy(r => (
                r.Header.ParentItemcode,
                HasProductNo: !string.IsNullOrWhiteSpace(r.Header.ProductNo),
                r.Final.Itemcodes,
                r.Final.Itemnames,
                r.Final.ManufacturingDates))
            .ToList();

        var rows = new List<PlanningPreparationCsvRow>();
        foreach (var grp in groups)
        {
            var (first, facility, final) = grp.First();
            var totalQty = grp.Sum(r => r.Header.Qty);

            var bomKey = (first.ParentItemcode, facility);
            if (!bomCache.TryGetValue(bomKey, out var boms))
            {
                boms = await _preparationWorkService.FetchBomsForParentAsync(first.ParentItemcode, AsOf(first), facility, ct);
                bomCache[bomKey] = boms;
            }

            var deliveryDate = FormatDate(first.NeedDate);
            var orderNo = string.Join("・", grp
                .Select(r => r.Header.OrderNo)
                .Where(o => o.Length > 0)
                .Distinct(StringComparer.Ordinal));

            if (boms.Count == 0)
            {
                rows.Add(new PlanningPreparationCsvRow
                {
                    OrderNo = orderNo,
                    FinalManufacturingDate = final.ManufacturingDates,
                    DeliveryDate = deliveryDate,
                    FinalItemcode = final.Itemcodes,
                    FinalItemname = final.Itemnames,
                    ParentItemcode = first.ParentItemcode,
                    ParentItemname = first.ParentItemname,
                    ProductionOrder = null
                });
                continue;
            }

            foreach (var b in boms)
            {
                rows.Add(new PlanningPreparationCsvRow
                {
                    OrderNo = orderNo,
                    FinalManufacturingDate = final.ManufacturingDates,
                    DeliveryDate = deliveryDate,
                    FinalItemcode = final.Itemcodes,
                    FinalItemname = final.Itemnames,
                    ParentItemcode = first.ParentItemcode,
                    ParentItemname = first.ParentItemname,
                    ChildItemcode = b.ChildItemcode,
                    ChildItemname = b.ChildItemname ?? "",
                    RequiredQtyInUnit0 = PreparationBomQuantity.ComputeRequiredQty(totalQty, b.InputQty, b.OutputQty, b.YieldPercent),
                    Unit = b.ChildUnitname ?? "",
                    ProductionOrder = b.ProductionOrder
                });
            }
        }

        // 子品目の購買単位（item.purchaseorderunitcode）へ換算。未設定などのときは単位0のまま。
        var unitInfos = await FetchPurchaseUnitInfosAsync(
            rows.Select(r => r.ChildItemcode.Trim()).Where(c => c.Length > 0).Distinct(StringComparer.Ordinal).ToArray(), ct);
        foreach (var r in rows)
        {
            if (r.RequiredQtyInUnit0 is not decimal qtyU0)
                continue;
            r.Quantity = ReportQuantityFormatter.FormatCeilingQuantity(qtyU0);
            if (unitInfos.TryGetValue(r.ChildItemcode.Trim(), out var u))
            {
                var (qtyPo, unitPo) = PurchaseOrderUnitConverter.FromUnit0(
                    qtyU0, u.PurchaseOrderUnitCode, u.UnitCode0, u.UnitCode1, u.UnitCode2, u.UnitCode3,
                    u.ConversionValue1, u.ConversionValue2, u.ConversionValue3, u.PurchaseOrderUnitName, r.Unit);
                r.PurchaseQuantity = ReportQuantityFormatter.FormatCeilingQuantity(qtyPo);
                r.PurchaseUnit = unitPo;
            }
            else
            {
                r.PurchaseQuantity = r.Quantity;
                r.PurchaseUnit = r.Unit;
            }
        }

        return SortCsvRows(rows);
    }

    private static DateOnly AsOf(PlanningOrderHeaderRow h) => h.NeedDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

    private static string FormatDate(DateOnly? d) => d?.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture) ?? "";

    /// <summary>並び順: 納期 → 最終製品の製造日 → 最終製品コード → 親品目コード → レシピ並び順（NULL は末尾）→ 子品目コード。</summary>
    internal static List<PlanningPreparationCsvRow> SortCsvRows(IReadOnlyList<PlanningPreparationCsvRow> rows)
    {
        return rows
            .OrderBy(r => r.DeliveryDate ?? "", StringComparer.Ordinal)
            .ThenBy(r => r.FinalManufacturingDate ?? "", StringComparer.Ordinal)
            .ThenBy(r => r.FinalItemcode ?? "", StringComparer.Ordinal)
            .ThenBy(r => r.ParentItemcode ?? "", StringComparer.Ordinal)
            .ThenBy(r => r.ProductionOrder ?? decimal.MaxValue)
            .ThenBy(r => r.ChildItemcode ?? "", StringComparer.Ordinal)
            .ToList();
    }

    public static byte[] WriteCsvUtf8Bom(IReadOnlyList<PlanningPreparationCsvRow> rows)
    {
        static string Esc(string? s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.Contains('"') || s.Contains(',') || s.Contains('\n') || s.Contains('\r'))
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }

        var sb = new StringBuilder();
        sb.AppendLine("注番,最終製品製造日,納期,最終製品コード,最終製品,親品目コード,親品目,子品目コード,子品目,数量,単位,購買単位数量,購買単位");
        foreach (var r in rows)
        {
            sb.Append(Esc(r.OrderNo)).Append(',')
                .Append(Esc(r.FinalManufacturingDate)).Append(',')
                .Append(Esc(r.DeliveryDate)).Append(',')
                .Append(Esc(r.FinalItemcode)).Append(',')
                .Append(Esc(r.FinalItemname)).Append(',')
                .Append(Esc(r.ParentItemcode)).Append(',')
                .Append(Esc(r.ParentItemname)).Append(',')
                .Append(Esc(r.ChildItemcode)).Append(',')
                .Append(Esc(r.ChildItemname)).Append(',')
                .Append(Esc(r.Quantity)).Append(',')
                .Append(Esc(r.Unit)).Append(',')
                .Append(Esc(r.PurchaseQuantity)).Append(',')
                .Append(Esc(r.PurchaseUnit))
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
    /// 最終製品と、その製造日（最終製品の計画オーダーの着手日。releasedate が無ければ needdate）。
    /// BOM を親方向に遡り、同じ productno の計画オーダーを持つ最上位の品目を最終製品とする。
    /// productno が無い・一致する計画オーダーが無い場合は BOM 上の最上位品目とし、製造日は空欄。
    /// </summary>
    private async Task<PlanningFinalProductInfo> ResolveFinalProductAsync(
        string parentItemcode,
        string productNo,
        DateOnly asOf,
        string facilityCode,
        CancellationToken ct)
    {
        if (productNo.Length > 0)
        {
            var matched = await _db.Database
                .SqlQueryRaw<PlanningPreparationWorkFinalProductOrderSqlRow>($"""
                    WITH RECURSIVE up AS (
                      SELECT @p::text AS itemcode, ARRAY[@p::text] AS path, 0 AS depth
                      UNION ALL
                      SELECT TRIM(b.parentitemcode), up.path || TRIM(b.parentitemcode), up.depth + 1
                      FROM up
                      INNER JOIN bom b ON b.childitemcode = up.itemcode
                      WHERE b.parentitemcode IS NOT NULL
                        AND TRIM(BOTH FROM COALESCE(b.facilitycode, '')) = @facility
                        AND (b.startdate IS NULL OR b.startdate <= @asof)
                        AND (b.enddate IS NULL OR b.enddate >= @asof)
                        AND NOT (TRIM(b.parentitemcode) = ANY (up.path))
                        AND CARDINALITY(up.path) < 20
                    ),
                    matched AS (
                      SELECT up.itemcode, up.depth, COALESCE(fo.releasedate, fo.needdate) AS mfg_date
                      FROM up
                      INNER JOIN planningorder fo ON fo.itemcode = up.itemcode
                        AND TRIM(fo.productno) = @productNo
                      WHERE TRUE
                        {DedupConditionSql("fo")}
                    )
                    SELECT
                      m.itemcode AS "Itemcode",
                      COALESCE(i.itemname, '') AS "Itemname",
                      m.mfg_date AS "ManufacturingDate"
                    FROM matched m
                    LEFT JOIN item i ON TRIM(i.itemcode) = m.itemcode
                    WHERE m.depth = (SELECT MAX(depth) FROM matched)
                    GROUP BY m.itemcode, i.itemname, m.mfg_date
                    ORDER BY 1, 3
                    """,
                    new NpgsqlParameter("p", NpgsqlDbType.Text) { Value = parentItemcode },
                    new NpgsqlParameter("facility", NpgsqlDbType.Text) { Value = facilityCode },
                    new NpgsqlParameter("asof", NpgsqlDbType.Date) { Value = asOf },
                    new NpgsqlParameter("productNo", NpgsqlDbType.Text) { Value = productNo })
                .ToListAsync(ct);

            if (matched.Count > 0)
            {
                var items = matched
                    .GroupBy(m => m.Itemcode, StringComparer.Ordinal)
                    .Select(g => (Code: g.Key, Name: g.First().Itemname))
                    .ToList();
                return new PlanningFinalProductInfo(
                    string.Join("、", items.Select(x => x.Code)),
                    string.Join("、", items.Select(x => x.Name)),
                    string.Join("、", matched
                        .Where(m => m.ManufacturingDate.HasValue)
                        .Select(m => m.ManufacturingDate!.Value)
                        .Distinct()
                        .OrderBy(d => d)
                        .Select(d => FormatDate(d))));
            }
        }

        var finals = await FetchFinalProductsAsync(parentItemcode, asOf, facilityCode, ct);
        return new PlanningFinalProductInfo(
            string.Join("、", finals.Select(f => f.Itemcode)),
            string.Join("、", finals.Select(f => f.Itemname)),
            "");
    }

    /// <summary>子品目の購買単位換算に必要な品目情報（<c>item.purchaseorderunitcode</c>・単位0～3・換算値）。</summary>
    private async Task<Dictionary<string, PlanningPurchaseUnitInfo>> FetchPurchaseUnitInfosAsync(string[] itemcodes, CancellationToken ct)
    {
        var result = new Dictionary<string, PlanningPurchaseUnitInfo>(StringComparer.Ordinal);
        if (itemcodes.Length == 0)
            return result;

        var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
        var shouldClose = conn.State != ConnectionState.Open;
        if (shouldClose)
            await conn.OpenAsync(ct);
        try
        {
            await using var cmd = new NpgsqlCommand(
                """
                SELECT
                  TRIM(i.itemcode),
                  i.purchaseorderunitcode,
                  i.unitcode0,
                  i.unitcode1,
                  i.unitcode2,
                  i.unitcode3,
                  i.conversionvalue1,
                  i.conversionvalue2,
                  i.conversionvalue3,
                  COALESCE(upo.unitname, '')
                FROM item i
                LEFT JOIN unit upo ON upo.unitcode = NULLIF(TRIM(i.purchaseorderunitcode), '')
                WHERE TRIM(i.itemcode) = ANY(@codes)
                """, conn);
            cmd.Parameters.Add(new NpgsqlParameter("codes", NpgsqlDbType.Text | NpgsqlDbType.Array)
            {
                Value = itemcodes
            });
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                result[reader.GetString(0)] = new PlanningPurchaseUnitInfo(
                    reader.IsDBNull(1) ? null : reader.GetString(1),
                    reader.IsDBNull(2) ? null : reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5),
                    reader.IsDBNull(6) ? null : reader.GetDecimal(6),
                    reader.IsDBNull(7) ? null : reader.GetDecimal(7),
                    reader.IsDBNull(8) ? null : reader.GetDecimal(8),
                    reader.GetString(9));
            }

            return result;
        }
        finally
        {
            if (shouldClose && conn.State == ConnectionState.Open)
                await conn.CloseAsync();
        }
    }

    private sealed record PlanningFinalProductInfo(string Itemcodes, string Itemnames, string ManufacturingDates);

    private sealed record PlanningPurchaseUnitInfo(
        string? PurchaseOrderUnitCode,
        string? UnitCode0,
        string? UnitCode1,
        string? UnitCode2,
        string? UnitCode3,
        decimal? ConversionValue1,
        decimal? ConversionValue2,
        decimal? ConversionValue3,
        string PurchaseOrderUnitName);

    private async Task<PlanningPreparationFilter> BuildFilterAsync(
        string delvedt,
        IReadOnlyList<string> manufacturingRouteCodes,
        string? itemcd,
        long[]? majorClassificationIds,
        long? middleClassificationId,
        IReadOnlyList<long> workcenterIds,
        IReadOnlyList<long> warehouseIds,
        CancellationToken ct)
    {
        var date = PreparationWorkService.ParseYyyymmdd(delvedt);
        if (!date.HasValue)
            throw new ArgumentException("納期はYYYYMMDD形式（8桁）で指定してください。", nameof(delvedt));

        var majorCodes = Array.Empty<string>();
        var majIds = (majorClassificationIds ?? Array.Empty<long>()).Where(id => id > 0).Distinct().ToArray();
        if (majIds.Length > 0)
        {
            var codes = await _db.MajorClassifications.AsNoTracking()
                .Where(m => majIds.Contains(m.MajorClassificationId))
                .Select(m => m.MajorClassificationCode)
                .ToListAsync(ct);
            majorCodes = codes.Where(c => !string.IsNullOrEmpty(c)).Select(c => c!).ToArray();
        }

        var middleCode = "";
        if (middleClassificationId is long midId and > 0)
        {
            var middle = await _db.MiddleClassifications.AsNoTracking()
                .FirstOrDefaultAsync(m => m.MiddleClassificationId == midId, ct);
            if (middle?.MiddleClassificationCode is { Length: > 0 } code)
                middleCode = code;
        }

        return new PlanningPreparationFilter(
            date.Value,
            (manufacturingRouteCodes ?? Array.Empty<string>())
                .Select(s => (s ?? "").Trim())
                .Where(s => s.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            (workcenterIds ?? Array.Empty<long>()).Where(id => id > 0).Distinct().ToArray(),
            (warehouseIds ?? Array.Empty<long>()).Where(id => id > 0).Distinct().ToArray(),
            itemcd?.Trim() ?? "",
            majorCodes,
            middleCode);
    }

    private async Task<List<PlanningOrderHeaderRow>> FetchPlanningOrderHeadersAsync(IReadOnlyList<long> planningOrderIds, CancellationToken ct)
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
                  po.planningorderid,
                  COALESCE(po.qty, 0) AS qty,
                  TRIM(BOTH FROM i.itemcode) AS parent_itemcode,
                  COALESCE(i.itemname, '') AS parent_itemname,
                  COALESCE(po.releasedate, po.needdate) AS need_date,
                  po.productno,
                  COALESCE(NULLIF(TRIM(po.facilitycode), ''), sol.facilitycode) AS facility_code,
                  (SELECT string_agg(ot.ordertableid::text, '・' ORDER BY ot.ordertableid)
                   FROM ordertable ot
                   WHERE ot.planningorderid = po.planningorderid) AS order_no
                FROM planningorder po
                INNER JOIN item i ON TRIM(BOTH FROM i.itemcode) = TRIM(BOTH FROM po.itemcode)
                LEFT JOIN salesorderline sol ON sol.salesorderlineid = po.salesorderlineid
                WHERE po.planningorderid = ANY(@ids)
                ORDER BY po.planningorderid
                """, conn);
            cmd.Parameters.Add(new NpgsqlParameter("ids", NpgsqlDbType.Bigint | NpgsqlDbType.Array)
            {
                Value = planningOrderIds.ToArray()
            });
            var list = new List<PlanningOrderHeaderRow>();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                list.Add(new PlanningOrderHeaderRow
                {
                    Planningorderid = reader.GetInt64(0),
                    Qty = reader.GetDecimal(1),
                    ParentItemcode = reader.GetString(2),
                    ParentItemname = reader.GetString(3),
                    NeedDate = ReadDateNullable(reader, 4),
                    ProductNo = reader.IsDBNull(5) ? null : reader.GetString(5),
                    FacilityCode = reader.IsDBNull(6) ? null : reader.GetString(6),
                    OrderNo = reader.IsDBNull(7) ? "" : reader.GetString(7)
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

    /// <summary>
    /// 親品目から BOM を親方向へ最上位まで遡り、最終製品（それ以上親を持たない品目）を返す。
    /// 親品目自身が BOM 上の最上位であれば親品目自身を最終製品とする。BOM の工場・有効期間で絞り込み、循環は経路で除外する。
    /// </summary>
    private async Task<List<PlanningPreparationWorkFinalProductSqlRow>> FetchFinalProductsAsync(
        string parentItemcode,
        DateOnly asOf,
        string facilityCode,
        CancellationToken ct)
    {
        return await _db.Database
            .SqlQuery<PlanningPreparationWorkFinalProductSqlRow>($@"
WITH RECURSIVE up AS (
  SELECT {parentItemcode}::text AS itemcode, ARRAY[{parentItemcode}::text] AS path
  UNION ALL
  SELECT TRIM(b.parentitemcode), up.path || TRIM(b.parentitemcode)
  FROM up
  INNER JOIN bom b ON b.childitemcode = up.itemcode
  WHERE b.parentitemcode IS NOT NULL
    AND TRIM(BOTH FROM COALESCE(b.facilitycode, '')) = {facilityCode}
    AND (b.startdate IS NULL OR b.startdate <= {asOf})
    AND (b.enddate IS NULL OR b.enddate >= {asOf})
    AND NOT (TRIM(b.parentitemcode) = ANY (up.path))
    AND CARDINALITY(up.path) < 20
)
SELECT DISTINCT
  up.itemcode AS ""Itemcode"",
  COALESCE(i.itemname, '') AS ""Itemname""
FROM up
LEFT JOIN item i ON TRIM(i.itemcode) = up.itemcode
WHERE NOT EXISTS (
  SELECT 1 FROM bom b2
  WHERE b2.childitemcode = up.itemcode
    AND b2.parentitemcode IS NOT NULL
    AND TRIM(BOTH FROM COALESCE(b2.facilitycode, '')) = {facilityCode}
    AND (b2.startdate IS NULL OR b2.startdate <= {asOf})
    AND (b2.enddate IS NULL OR b2.enddate >= {asOf})
)
ORDER BY 1
")
            .ToListAsync(ct);
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

    private sealed record PlanningPreparationFilter(
        DateOnly Date,
        string[] Routes,
        long[] WorkcenterIds,
        long[] WarehouseIds,
        string ItemFilter,
        string[] MajorCodes,
        string MiddleCode)
    {
        /// <summary>SQL パラメータ（型を明示して NULL・空配列の型不明エラーを回避）。クエリごとに新しいインスタンスを返す。</summary>
        public NpgsqlParameter[] ToParameters() => new[]
        {
            new NpgsqlParameter("date", NpgsqlDbType.Date) { Value = Date },
            new NpgsqlParameter("routes", NpgsqlDbType.Text | NpgsqlDbType.Array) { Value = Routes },
            new NpgsqlParameter("wcIds", NpgsqlDbType.Bigint | NpgsqlDbType.Array) { Value = WorkcenterIds },
            new NpgsqlParameter("whIds", NpgsqlDbType.Bigint | NpgsqlDbType.Array) { Value = WarehouseIds },
            new NpgsqlParameter("itemF", NpgsqlDbType.Text) { Value = ItemFilter },
            new NpgsqlParameter("majorCodes", NpgsqlDbType.Text | NpgsqlDbType.Array) { Value = MajorCodes },
            new NpgsqlParameter("middleCode", NpgsqlDbType.Text) { Value = MiddleCode }
        };
    }
}

public sealed class PlanningPreparationCsvRow
{
    /// <summary>注番（<c>ordertable.ordertableid</c>。合算した場合は「・」区切り、紐づかなければ空）。</summary>
    public string OrderNo { get; set; } = "";
    /// <summary>最終製品の製造日（最終製品の計画オーダーの着手日。複数あれば「、」区切り）。</summary>
    public string FinalManufacturingDate { get; set; } = "";
    /// <summary>納期（親品目の計画オーダーの着手日＝子品目の使用日）。</summary>
    public string DeliveryDate { get; set; } = "";
    /// <summary>最終製品コード（複数あれば「、」区切り）。</summary>
    public string FinalItemcode { get; set; } = "";
    /// <summary>最終製品名称（複数あれば「、」区切り）。</summary>
    public string FinalItemname { get; set; } = "";
    public string ParentItemcode { get; set; } = "";
    public string ParentItemname { get; set; } = "";
    public string ChildItemcode { get; set; } = "";
    public string ChildItemname { get; set; } = "";
    public string Quantity { get; set; } = "";
    public string Unit { get; set; } = "";
    /// <summary>購買単位（<c>item.purchaseorderunitcode</c>）換算の数量。未設定などのときは単位0の数量。</summary>
    public string PurchaseQuantity { get; set; } = "";
    public string PurchaseUnit { get; set; } = "";

    /// <summary>子品目の所要量（単位0、丸め前）。BOM の無い行は null。CSV の列には出力しない。</summary>
    public decimal? RequiredQtyInUnit0 { get; set; }

    /// <summary>並び順用: レシピ（BOM）の並び順（<c>bom.productionorder</c>）。NULL は末尾。CSV の列には出力しない。</summary>
    public decimal? ProductionOrder { get; set; }
}

internal sealed class PlanningOrderHeaderRow
{
    public long Planningorderid { get; set; }
    public decimal Qty { get; set; }
    public string ParentItemcode { get; set; } = "";
    public string ParentItemname { get; set; } = "";
    /// <summary>納期（<c>planningorder.releasedate</c>、NULL なら needdate）。</summary>
    public DateOnly? NeedDate { get; set; }
    public string? ProductNo { get; set; }
    /// <summary><c>planningorder.facilitycode</c>（未設定なら受注明細の工場コード）。</summary>
    public string? FacilityCode { get; set; }
    /// <summary>注番（<c>planningorderid</c> で紐づく <c>ordertable.ordertableid</c>）。紐づかなければ空。</summary>
    public string OrderNo { get; set; } = "";
}
