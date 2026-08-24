using System.Data;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using BaggingInstructions.Api.Core;
using BaggingInstructions.Api.DTOs;
using BaggingInstructions.Api.QueryResults;

namespace BaggingInstructions.Api.Services;

/// <summary>
/// 現品票印刷（新）：子品目起点の検索・印刷行生成。
/// 起点は「ordertype='MO' かつ BOM の childitemcode に存在しない品目（=最上位の完成品）」で、
/// そこから BOM を再帰探索（最大10階層）し、直接の子だけでなく孫以下も 1行=1子品目 として返す。
/// 便は ordertable に列が無いため productno から解決する（既存の製造指示書と同じ SLOT-CHAIN 方式）。
/// </summary>
public sealed class ProductLabelNewService
{
    /// <summary>BOM 再帰探索の最大階層。</summary>
    private const int MaxBomDepth = 10;

    private readonly AppDbContext _db;

    public ProductLabelNewService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>productno（拠点|日付|便|…|品目）から製造便コードを取り出す式。</summary>
    private static string SlotFromProductNo(string alias) =>
        $"NULLIF(TRIM(COALESCE(CASE WHEN CARDINALITY(STRING_TO_ARRAY({alias}.productno, '|')) >= 5 "
        + $"THEN SPLIT_PART({alias}.productno, '|', 3) ELSE SPLIT_PART({alias}.productno, '|', 2) END, '')), '')";

    /// <summary>
    /// 便の解決（CROSS JOIN LATERAL）。自オーダーが製番品ならその便。
    /// [SLOT-CHAIN] 親を遡るのは親自身が製番品（productno 非空）のときのみ。
    /// </summary>
    private static string SlotLateralSql() => $"""
        CROSS JOIN LATERAL (
          SELECT COALESCE(
            {SlotFromProductNo("ot")},
            CASE WHEN COALESCE(TRIM(parent_ot.productno), '') <> ''
                 THEN {SlotFromProductNo("parent_ot")} END,
            CASE WHEN COALESCE(TRIM(parent_ot.productno), '') <> '' AND COALESCE(TRIM(gp_ot.productno), '') <> ''
                 THEN {SlotFromProductNo("gp_ot")} END,
            ''
          ) AS slotcode
        ) sc
        """;

    /// <summary>起点（最上位完成品オーダ）の抽出条件。</summary>
    private sealed class RootFilter
    {
        /// <summary>納期（ordertable.releasedate と完全一致）。OrderTableIds 指定時は不要。</summary>
        public DateOnly? NeedDate { get; init; }
        /// <summary>ordertableid 直接指定（印刷時）。指定時は他の絞り込みを行わない。</summary>
        public long[]? OrderTableIds { get; init; }
        public string? MajorCode { get; init; }
        public string? ItemCode { get; init; }
        public long? WorkcenterId { get; init; }
        public long? WarehouseId { get; init; }
        public string[]? SlotCodes { get; init; }
    }

    /// <summary>roots の前に置く CTE（dedup）と roots 本体の SQL。</summary>
    private sealed record RootsSql(string PreambleCtes, string RootsSelect);

    /// <summary>起点オーダを取り出す非再帰 CTE（roots）の SQL を組み立て、cmd にパラメータを追加する。</summary>
    private static RootsSql BuildRootsCteSql(RootFilter f, NpgsqlCommand cmd)
    {
        var byIds = f.OrderTableIds is { Length: > 0 };
        var joinWh = !byIds && f.WarehouseId.HasValue && f.WarehouseId.Value > 0;
        var facility = BomFacility.SqlResolve("sol.facilitycode");

        var sql = new StringBuilder();
        sql.AppendLine($"""
            SELECT
              ot.ordertableid,
              COALESCE(ot.releasedate, ot.needdate)                AS releasedate,
              COALESCE(ot.itemcode, '')                            AS root_itemcode,
              COALESCE(i.itemname, '')                             AS root_itemname,
              COALESCE(ot.qty, 0::numeric)                         AS root_qty,
              {facility}                                           AS facilitycode,
              COALESCE(ot.releasedate, ot.needdate, CURRENT_DATE)  AS bom_asof,
              COALESCE(sc.slotcode, '')                            AS slotcode,
              COALESCE(NULLIF(TRIM(ds.slotname), ''), NULLIF(sc.slotcode, ''), '') AS slotname,
              COALESCE(w.workcentername, '')                       AS workcentername
            FROM ordertable ot
            INNER JOIN item i ON i.itemcode = ot.itemcode
            LEFT JOIN salesorderline sol ON sol.salesorderlineid = ot.salesorderlineid
            LEFT JOIN workcenter w ON (
              w.workcentercode = TRIM(BOTH FROM ot.workcentercode)
              OR w.workcenterid::text = TRIM(BOTH FROM ot.workcentercode)
            )
            LEFT JOIN ordertable parent_ot ON parent_ot.ordertableid = ot.parentordertableid
            LEFT JOIN ordertable gp_ot ON gp_ot.ordertableid = parent_ot.parentordertableid
            {SlotLateralSql()}
            LEFT JOIN deliveryslot ds ON ds.slotcode = sc.slotcode
            """);
        if (joinWh)
            sql.AppendLine("LEFT JOIN warehouses wh ON TRIM(COALESCE(wh.warehousecode, '')) = TRIM(COALESCE(i.warehousecode, ''))");

        if (byIds)
        {
            // 印刷時：画面で選択済みの ordertableid をそのまま起点にする（検索時に条件判定済み）。
            sql.AppendLine("WHERE ot.ordertableid = ANY(@ids)");
            cmd.Parameters.Add(new NpgsqlParameter("ids", NpgsqlDbType.Bigint | NpgsqlDbType.Array) { Value = f.OrderTableIds! });
            return new RootsSql("", sql.ToString());
        }

        sql.AppendLine("WHERE ot.releasedate = @needDate");
        cmd.Parameters.AddWithValue("needDate", f.NeedDate!.Value);
        sql.AppendLine("AND TRIM(COALESCE(ot.ordertype, '')) = 'MO'");
        // 最上位の完成品のみ（BOM の子側に現れない品目）
        sql.AppendLine($"""
            AND NOT EXISTS (
              SELECT 1 FROM bom b2
              WHERE b2.childitemcode = ot.itemcode
                AND TRIM(BOTH FROM COALESCE(b2.facilitycode, '')) = {facility}
                AND (b2.startdate IS NULL OR b2.startdate <= @needDate)
                AND (b2.enddate IS NULL OR b2.enddate >= @needDate)
            )
            """);

        if (f.MajorCode != null)
        {
            sql.AppendLine("AND TRIM(COALESCE(i.majorclassificationcode, '')) = TRIM(@majorCode)");
            cmd.Parameters.AddWithValue("majorCode", f.MajorCode.Trim());
        }
        if (!string.IsNullOrWhiteSpace(f.ItemCode))
        {
            sql.AppendLine("AND TRIM(COALESCE(ot.itemcode, '')) ILIKE @itemCodePattern");
            cmd.Parameters.AddWithValue("itemCodePattern", $"%{f.ItemCode.Trim()}%");
        }
        if (f.WorkcenterId.HasValue && f.WorkcenterId.Value > 0)
        {
            sql.AppendLine("AND w.workcenterid = @workcenterId");
            cmd.Parameters.AddWithValue("workcenterId", f.WorkcenterId.Value);
        }
        if (joinWh)
        {
            sql.AppendLine("AND wh.warehouseid = @warehouseId");
            cmd.Parameters.AddWithValue("warehouseId", f.WarehouseId!.Value);
        }
        if (f.SlotCodes is { Length: > 0 })
        {
            sql.AppendLine("AND COALESCE(sc.slotcode, '') = ANY(@slots)");
            cmd.Parameters.Add(new NpgsqlParameter("slots", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = f.SlotCodes });
        }
        // [DEDUP-productno] 同一品目・同一productno（同一実効日付）は最新ordertableidのみ採用（MRP重複対策）。
        // 起点は releasedate = @needDate に限定されるため実効日付は必ず @needDate であり、
        // 相関サブクエリではなく納期単位の集約（dedup CTE）で同じ結果を得られる。
        // AS MATERIALIZED は必須：インライン化されると行ごとに再集計され極端に遅くなる。
        sql.AppendLine("""
            AND (
              COALESCE(TRIM(ot.productno), '') = ''
              OR ot.ordertableid = (
                SELECT d.max_ordertableid FROM dedup d
                WHERE d.productno = TRIM(ot.productno)
                  AND d.itemcode = TRIM(ot.itemcode)
              )
            )
            """);

        var dedupCte = """
            dedup AS MATERIALIZED (
              SELECT
                TRIM(o2.productno)    AS productno,
                TRIM(o2.itemcode)     AS itemcode,
                MAX(o2.ordertableid)  AS max_ordertableid
              FROM ordertable o2
              WHERE COALESCE(o2.releasedate, o2.needdate) = @needDate
                AND COALESCE(TRIM(o2.productno), '') <> ''
              GROUP BY 1, 2
            ),
            """;
        return new RootsSql(dedupCte + "\n", sql.ToString());
    }

    /// <summary>roots から BOM を下へ再帰探索する CTE（bom_tree）の SQL。</summary>
    private static string BuildBomTreeCteSql() => $"""
        SELECT
          r.ordertableid,
          r.releasedate,
          r.root_itemcode,
          r.root_itemname,
          r.root_qty,
          r.root_qty      AS accumulated_qty,
          r.root_itemcode AS current_itemcode,
          r.facilitycode,
          r.bom_asof,
          r.slotcode,
          r.slotname,
          r.workcentername,
          0               AS depth
        FROM roots r
        UNION ALL
        SELECT
          bt.ordertableid,
          bt.releasedate,
          bt.root_itemcode,
          bt.root_itemname,
          bt.root_qty,
          CASE WHEN COALESCE(b.outputqty, 0::numeric) <> 0
            THEN bt.accumulated_qty * COALESCE(b.inputqty, 0::numeric) / b.outputqty
            ELSE COALESCE(b.inputqty, 0::numeric)
          END,
          b.childitemcode,
          bt.facilitycode,
          bt.bom_asof,
          bt.slotcode,
          bt.slotname,
          bt.workcentername,
          bt.depth + 1
        FROM bom_tree bt
        INNER JOIN bom b ON b.parentitemcode = bt.current_itemcode
          AND b.childitemcode IS NOT NULL
          -- BOM は受注明細の工場コードで絞り込む（未設定なら MATSUYAMA）
          AND TRIM(BOTH FROM COALESCE(b.facilitycode, '')) = bt.facilitycode
          AND (b.startdate IS NULL OR b.startdate <= bt.bom_asof)
          AND (b.enddate IS NULL OR b.enddate >= bt.bom_asof)
        WHERE bt.depth < {MaxBomDepth}
        """;

    /// <summary>子孫品目行を取り出す最終 SELECT。conds は bt / ci に対する追加条件。</summary>
    private static string BuildChildSelectSql(IReadOnlyList<string> conds)
    {
        var where = conds.Count > 0 ? "\n  AND " + string.Join("\n  AND ", conds) : "";
        return $"""
            SELECT DISTINCT ON (bt.ordertableid, bt.current_itemcode)
              bt.ordertableid,
              bt.releasedate,
              bt.root_itemcode,
              bt.root_itemname,
              bt.root_qty,
              bt.workcentername,
              bt.current_itemcode,
              COALESCE(ci.itemname, ''),
              bt.accumulated_qty,
              COALESCE(NULLIF(TRIM(COALESCE(cu.unitname, cu.unitsymbol, '')), ''), ''),
              COALESCE(ci.shelflifedays, 0),
              bt.depth,
              bt.slotcode,
              bt.slotname
            FROM bom_tree bt
            INNER JOIN item ci ON ci.itemcode = bt.current_itemcode
            LEFT JOIN unit cu ON cu.unitcode = ci.unitcode0
            WHERE bt.depth > 0{where}
            ORDER BY bt.ordertableid, bt.current_itemcode, bt.depth
            """;
    }

    private static ProductLabelNewChildSqlRow ReadChildRow(NpgsqlDataReader reader) => new()
    {
        OrderTableId   = reader.GetInt64(0),
        ReleaseDate    = ReadDateOnlyNullable(reader, 1),
        ParentItemCode = reader.GetString(2),
        ParentItemName = reader.GetString(3),
        Qty            = reader.GetDecimal(4),
        WorkcenterName = reader.GetString(5),
        ChildItemCode  = reader.GetString(6),
        ChildItemName  = reader.GetString(7),
        ChildQty       = reader.GetDecimal(8),
        ChildUnitName  = reader.GetString(9),
        ShelflifeDays  = reader.GetInt32(10),
        Depth          = reader.GetInt32(11),
        SlotCode       = reader.GetString(12),
        SlotName       = reader.GetString(13),
    };

    /// <summary>
    /// 便マスタ（複数選択プルダウン用）。指定納期の起点オーダ（MO・最上位完成品）に現れる便のみ。
    /// </summary>
    public async Task<List<ProductionInstructionSlotOptionDto>> ListSlotsAsync(string needDateYyyymmdd, CancellationToken ct = default)
    {
        var date = ParseYyyymmdd(needDateYyyymmdd);
        if (!date.HasValue)
            throw new ArgumentException("納期はYYYYMMDD形式（8桁）で指定してください。", nameof(needDateYyyymmdd));

        var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
        var shouldClose = conn.State != ConnectionState.Open;
        if (shouldClose) await conn.OpenAsync(ct);
        try
        {
            await using var cmd = new NpgsqlCommand("", conn);
            var roots = BuildRootsCteSql(new RootFilter { NeedDate = date.Value }, cmd);
            cmd.CommandText = $"""
                WITH {roots.PreambleCtes}roots AS (
                {roots.RootsSelect}
                )
                SELECT DISTINCT roots.slotcode, roots.slotname
                FROM roots
                WHERE COALESCE(roots.slotcode, '') <> ''
                ORDER BY 1
                """;

            var list = new List<ProductionInstructionSlotOptionDto>();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                list.Add(new ProductionInstructionSlotOptionDto
                {
                    Code = reader.GetString(0),
                    Name = reader.GetString(1),
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
    /// 現品票印刷（新）：納期の最上位完成品から BOM を再帰探索し、子孫品目を1行として返す。
    /// 子品目条件（コード部分一致・大分類・中分類・倉庫）は「表示する子品目そのもの」への絞り込み。
    /// 親品目条件（大分類・品目コード・作業区・倉庫）と便は起点オーダへの絞り込み。
    /// </summary>
    public async Task<List<ProductLabelNewRowDto>> SearchAsync(
        string needDateYyyymmdd,
        long? majorClassificationId,
        string? itemCode,
        long? workcenterId,
        long? warehouseId,
        string? childItemCode,
        long? childMajorClassificationId,
        long? childMiddleClassificationId,
        long? childWarehouseId,
        IReadOnlyList<string>? slotCodes,
        CancellationToken ct = default)
    {
        var date = ParseYyyymmdd(needDateYyyymmdd);
        if (!date.HasValue)
            throw new ArgumentException("納期はYYYYMMDD形式（8桁）で指定してください。", nameof(needDateYyyymmdd));

        string? majorCode = null;
        if (majorClassificationId.HasValue && majorClassificationId.Value > 0)
        {
            majorCode = await LookupMajorCodeAsync(majorClassificationId.Value, ct);
            if (string.IsNullOrEmpty(majorCode))
                return new List<ProductLabelNewRowDto>();
        }

        var child = await ResolveChildCriteriaAsync(
            childItemCode, childMajorClassificationId, childMiddleClassificationId, childWarehouseId, ct);
        if (child.NoMatch)
            return new List<ProductLabelNewRowDto>();

        var slots = (slotCodes ?? Array.Empty<string>())
            .Select(s => (s ?? "").Trim())
            .Where(s => s.Length > 0)
            .Distinct()
            .ToArray();

        var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
        var shouldClose = conn.State != ConnectionState.Open;
        if (shouldClose) await conn.OpenAsync(ct);
        try
        {
            await using var cmd = new NpgsqlCommand("", conn);
            var roots = BuildRootsCteSql(new RootFilter
            {
                NeedDate = date.Value,
                MajorCode = majorCode,
                ItemCode = itemCode,
                WorkcenterId = workcenterId,
                WarehouseId = warehouseId,
                SlotCodes = slots.Length > 0 ? slots : null,
            }, cmd);

            cmd.CommandText = $"""
                WITH RECURSIVE {roots.PreambleCtes}roots AS (
                {roots.RootsSelect}
                ),
                bom_tree AS (
                {BuildBomTreeCteSql()}
                )
                {BuildChildSelectSql(BuildChildConditions(child, cmd))}
                """;

            var raw = new List<ProductLabelNewChildSqlRow>();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                raw.Add(ReadChildRow(reader));

            return AggregateRows(raw);
        }
        finally
        {
            if (shouldClose && conn.State == ConnectionState.Open)
                await conn.CloseAsync();
        }
    }

    /// <summary>
    /// 印刷用：指定 ordertableid を起点に BOM を再帰探索した子孫品目行（合算前）。
    /// instructionType 指定時は子品目コードの先頭2桁で絞り込む。
    /// </summary>
    public async Task<List<ProductLabelNewChildSqlRow>> LoadChildRowsByOrderTableIdsAsync(
        IReadOnlyList<long> orderTableIds,
        string? instructionType,
        CancellationToken ct = default)
    {
        var ids = (orderTableIds ?? Array.Empty<long>()).Where(id => id > 0).Distinct().ToArray();
        if (ids.Length == 0)
            return new List<ProductLabelNewChildSqlRow>();

        var conds = new List<string>();
        var typeFilter = InstructionTypeCondition(instructionType);
        if (typeFilter != null) conds.Add(typeFilter);

        var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
        var shouldClose = conn.State != ConnectionState.Open;
        if (shouldClose) await conn.OpenAsync(ct);
        try
        {
            await using var cmd = new NpgsqlCommand("", conn);
            var roots = BuildRootsCteSql(new RootFilter { OrderTableIds = ids }, cmd);
            cmd.CommandText = $"""
                WITH RECURSIVE {roots.PreambleCtes}roots AS (
                {roots.RootsSelect}
                ),
                bom_tree AS (
                {BuildBomTreeCteSql()}
                )
                {BuildChildSelectSql(conds)}
                """;

            var list = new List<ProductLabelNewChildSqlRow>();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                list.Add(ReadChildRow(reader));
            return list;
        }
        finally
        {
            if (shouldClose && conn.State == ConnectionState.Open)
                await conn.CloseAsync();
        }
    }

    /// <summary>印刷1ラベル分（行＋枚数）。</summary>
    public sealed record PrintRow(ProductLabelOrderSqlRow Row, int Count);

    /// <summary>
    /// 画面でチェックされた行（親×子品目）から印刷ラベル行を組み立てる。
    /// 数量は画面表示と同じく、行に含まれる全 ordertableid の所要数量合計。
    /// 指示書種別に一致しない子品目は対象外。
    /// </summary>
    public async Task<List<PrintRow>> BuildPrintRowsAsync(
        IReadOnlyList<ProductLabelNewPrintItemDto> items,
        string? instructionType,
        int defaultLabelCount,
        CancellationToken ct = default)
    {
        var targets = (items ?? Array.Empty<ProductLabelNewPrintItemDto>())
            .Where(it => it != null && it.OrderTableIds is { Count: > 0 } && !string.IsNullOrWhiteSpace(it.ChildItemCode))
            .ToList();
        if (targets.Count == 0)
            return new List<PrintRow>();

        var allIds = targets.SelectMany(t => t.OrderTableIds).Where(id => id > 0).Distinct().ToList();
        var rows = await LoadChildRowsByOrderTableIdsAsync(allIds, instructionType, ct);
        if (rows.Count == 0)
            return new List<PrintRow>();

        var index = new Dictionary<(long, string), ProductLabelNewChildSqlRow>();
        foreach (var r in rows)
            index[(r.OrderTableId, r.ChildItemCode.Trim())] = r;

        var result = new List<PrintRow>(targets.Count);
        foreach (var t in targets)
        {
            var childCode = t.ChildItemCode.Trim();
            var matched = t.OrderTableIds
                .Where(id => id > 0)
                .Distinct()
                .Select(id => index.TryGetValue((id, childCode), out var r) ? r : null)
                .Where(r => r != null)
                .Select(r => r!)
                .ToList();
            if (matched.Count == 0)
                continue;   // 指示書種別に一致しない、または BOM 変更で見つからない行

            var first = matched[0];
            var merged = new ProductLabelOrderSqlRow
            {
                OrderTableId   = first.OrderTableId,
                ReleaseDate    = first.ReleaseDate,
                ParentItemCode = first.ParentItemCode,
                ParentItemName = first.ParentItemName,
                Qty            = matched.Sum(r => r.Qty),
                WorkcenterName = first.WorkcenterName,
                ChildItemCode  = first.ChildItemCode,
                ChildItemName  = first.ChildItemName,
                ChildQty       = matched.Sum(r => r.ChildQty),
                ChildUnitName  = first.ChildUnitName,
                ShelflifeDays  = first.ShelflifeDays,
            };
            result.Add(new PrintRow(merged, Math.Max(1, t.Count ?? defaultLabelCount)));
        }
        return result;
    }

    /// <summary>指示書種別 → 子品目コード先頭2桁の条件。既存の現品票と同じ判定。</summary>
    private static string? InstructionTypeCondition(string? instructionType) =>
        instructionType?.Trim().ToLowerInvariant() switch
        {
            "cut"       => "LEFT(TRIM(COALESCE(bt.current_itemcode, '')), 2) IN ('50', '51', '53')",
            "seasoning" => "LEFT(TRIM(COALESCE(bt.current_itemcode, '')), 2) = '55'",
            "cooking"   => "LEFT(TRIM(COALESCE(bt.current_itemcode, '')), 2) <> '50'",
            _           => null,
        };

    /// <summary>同一（親品目コード, 子品目コード, 便）の行を合算し、数量を合計する。</summary>
    private static List<ProductLabelNewRowDto> AggregateRows(List<ProductLabelNewChildSqlRow> rows)
    {
        var positionMap = new Dictionary<(string, string, string), int>();
        for (var i = 0; i < rows.Count; i++)
        {
            var key = (rows[i].ParentItemCode, rows[i].ChildItemCode, rows[i].SlotCode);
            if (!positionMap.ContainsKey(key))
                positionMap[key] = i;
        }

        return rows
            .GroupBy(r => (r.ParentItemCode, r.ChildItemCode, r.SlotCode))
            .OrderBy(g => positionMap[g.Key])
            .Select(g =>
            {
                var ordered = g.OrderBy(r => r.OrderTableId).ToList();
                var first = ordered[0];
                return new ProductLabelNewRowDto
                {
                    OrderTableIds  = ordered.Select(r => r.OrderTableId).ToList(),
                    ReleaseDate    = first.ReleaseDate?.ToString("yyyyMMdd") ?? "",
                    ParentItemCode = first.ParentItemCode,
                    ParentItemName = first.ParentItemName,
                    ChildItemCode  = first.ChildItemCode,
                    ChildItemName  = first.ChildItemName,
                    Depth          = ordered.Min(r => r.Depth),
                    Qty            = ordered.Sum(r => r.ChildQty),
                    UnitName       = first.ChildUnitName,
                    WorkcenterName = first.WorkcenterName,
                    SlotCode       = first.SlotCode,
                    SlotName       = first.SlotName,
                };
            })
            .ToList();
    }

    /// <summary>子品目条件（マスタ ID をコードへ解決した状態）。</summary>
    private sealed class ChildCriteria
    {
        public string? ItemCode { get; init; }
        public string? MajorCode { get; init; }
        public string? MiddleCode { get; init; }
        public string? WarehouseCode { get; init; }
        public bool NoMatch { get; init; }
    }

    private async Task<string?> LookupMajorCodeAsync(long id, CancellationToken ct) =>
        await _db.MajorClassifications.AsNoTracking()
            .Where(m => m.MajorClassificationId == id)
            .Select(m => m.MajorClassificationCode)
            .FirstOrDefaultAsync(ct);

    /// <summary>子品目条件のマスタ ID をコードへ解決する。該当マスタなしの場合は NoMatch=true。</summary>
    private async Task<ChildCriteria> ResolveChildCriteriaAsync(
        string? childItemCode,
        long? childMajorClassificationId,
        long? childMiddleClassificationId,
        long? childWarehouseId,
        CancellationToken ct)
    {
        string? majorCode = null;
        if (childMajorClassificationId.HasValue && childMajorClassificationId.Value > 0)
        {
            majorCode = await LookupMajorCodeAsync(childMajorClassificationId.Value, ct);
            if (string.IsNullOrEmpty(majorCode))
                return new ChildCriteria { NoMatch = true };
        }

        string? middleCode = null;
        if (childMiddleClassificationId.HasValue && childMiddleClassificationId.Value > 0)
        {
            middleCode = await _db.MiddleClassifications.AsNoTracking()
                .Where(m => m.MiddleClassificationId == childMiddleClassificationId.Value)
                .Select(m => m.MiddleClassificationCode)
                .FirstOrDefaultAsync(ct);
            if (string.IsNullOrEmpty(middleCode))
                return new ChildCriteria { NoMatch = true };
        }

        string? warehouseCode = null;
        if (childWarehouseId.HasValue && childWarehouseId.Value > 0)
        {
            warehouseCode = await _db.Warehouses.AsNoTracking()
                .Where(w => w.WarehouseId == childWarehouseId.Value)
                .Select(w => w.WarehouseCode)
                .FirstOrDefaultAsync(ct);
            if (string.IsNullOrEmpty(warehouseCode))
                return new ChildCriteria { NoMatch = true };
        }

        return new ChildCriteria
        {
            ItemCode = string.IsNullOrWhiteSpace(childItemCode) ? null : childItemCode.Trim(),
            MajorCode = majorCode,
            MiddleCode = middleCode,
            WarehouseCode = warehouseCode,
        };
    }

    /// <summary>子品目条件を bom_tree（bt）／item（ci）に対する WHERE 条件へ展開する。</summary>
    private static List<string> BuildChildConditions(ChildCriteria c, NpgsqlCommand cmd)
    {
        var conds = new List<string>();
        if (!string.IsNullOrWhiteSpace(c.ItemCode))
        {
            conds.Add("bt.current_itemcode ILIKE @childItemCodePattern");
            cmd.Parameters.AddWithValue("childItemCodePattern", $"%{c.ItemCode}%");
        }
        if (c.MajorCode != null)
        {
            conds.Add("TRIM(COALESCE(ci.majorclassificationcode, '')) = TRIM(@childMajorCode)");
            cmd.Parameters.AddWithValue("childMajorCode", c.MajorCode.Trim());
        }
        if (c.MiddleCode != null)
        {
            conds.Add("TRIM(COALESCE(ci.middleclassificationcode, '')) = TRIM(@childMiddleCode)");
            cmd.Parameters.AddWithValue("childMiddleCode", c.MiddleCode.Trim());
        }
        if (c.WarehouseCode != null)
        {
            conds.Add("TRIM(COALESCE(ci.warehousecode, '')) = TRIM(@childWarehouseCode)");
            cmd.Parameters.AddWithValue("childWarehouseCode", c.WarehouseCode.Trim());
        }
        return conds;
    }

    private static DateOnly? ParseYyyymmdd(string? yyyymmdd)
    {
        if (string.IsNullOrEmpty(yyyymmdd) || yyyymmdd.Length != 8) return null;
        if (int.TryParse(yyyymmdd.AsSpan(0, 4), out var y)
            && int.TryParse(yyyymmdd.AsSpan(4, 2), out var m)
            && int.TryParse(yyyymmdd.AsSpan(6, 2), out var d))
        {
            try { return new DateOnly(y, m, d); } catch { return null; }
        }
        return null;
    }

    private static DateOnly? ReadDateOnlyNullable(NpgsqlDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal)) return null;
        var o = reader.GetValue(ordinal);
        return o switch
        {
            DateOnly d => d,
            DateTime dt => DateOnly.FromDateTime(dt),
            _ => null,
        };
    }
}
