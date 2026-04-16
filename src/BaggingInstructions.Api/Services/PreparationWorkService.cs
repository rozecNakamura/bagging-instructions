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

    public async Task<List<PreparationWorkGroupDto>> SearchGroupsAsync(
        string delvedt,
        string? slot,
        string? itemcd,
        long? majorClassificationId,
        long? middleClassificationId,
        CancellationToken ct = default)
    {
        var date = ParseYyyymmdd(delvedt);
        if (!date.HasValue)
            throw new ArgumentException("納期はYYYYMMDD形式（8桁）で指定してください。", nameof(delvedt));

        var slotF = slot?.Trim() ?? "";
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
  TO_CHAR(sol.planneddeliverydate, 'YYYYMMDD') AS ""Delvedt"",
  COALESCE(mc.majorclassificationcode, '') AS ""MajorCode"",
  COALESCE(mc.majorclassificationname, '') AS ""MajorName"",
  COALESCE(mid.middleclassificationcode, '') AS ""MiddleCode"",
  COALESCE(mid.middleclassificationname, '') AS ""MiddleName"",
  COUNT(*)::int AS ""LineCount""
FROM salesorderline sol
INNER JOIN item i ON i.itemcode = sol.itemcode
LEFT JOIN majorclassification mc ON mc.majorclassificationcode = i.majorclassficationcode
LEFT JOIN middleclassification mid ON mid.majorclassificationcode = i.majorclassficationcode
  AND mid.middleclassificationcode = i.middleclassficationcode
LEFT JOIN deliveryslot ds ON ds.slotcode = sol.slotcode
WHERE sol.planneddeliverydate = {date.Value}
  AND ({slotF} = '' OR COALESCE(ds.slotcode, '') ILIKE '%' || {slotF} || '%' OR COALESCE(ds.slotname, '') ILIKE '%' || {slotF} || '%')
  AND ({itemF} = '' OR i.itemcode ILIKE '%' || {itemF} || '%')
  AND ({majorCodeFilter} = '' OR mc.majorclassificationcode = {majorCodeFilter})
  AND ({middleCodeFilter} = '' OR mid.middleclassificationcode = {middleCodeFilter})
GROUP BY
  TO_CHAR(sol.planneddeliverydate, 'YYYYMMDD'),
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

        var slotF = filter.Slot?.Trim() ?? "";
        var itemF = filter.Itemcd?.Trim() ?? "";

        var all = new HashSet<long>();
        foreach (var key in groupKeys)
        {
            var maj = key.MajorClassificationCode ?? "";
            var mid = key.MiddleClassificationCode ?? "";
            var ids = await _db.Database
                .SqlQuery<PreparationWorkLineIdSqlRow>($@"
SELECT sol.salesorderlineid AS ""Salesorderlineid""
FROM salesorderline sol
INNER JOIN item i ON i.itemcode = sol.itemcode
LEFT JOIN majorclassification mc ON mc.majorclassificationcode = i.majorclassficationcode
LEFT JOIN middleclassification midt ON midt.majorclassificationcode = i.majorclassficationcode
  AND midt.middleclassificationcode = i.middleclassficationcode
LEFT JOIN deliveryslot ds ON ds.slotcode = sol.slotcode
WHERE sol.planneddeliverydate = {date.Value}
  AND TO_CHAR(sol.planneddeliverydate, 'YYYYMMDD') = {key.Delvedt}
  AND COALESCE(mc.majorclassificationcode, '') = {maj}
  AND COALESCE(midt.middleclassificationcode, '') = {mid}
  AND ({slotF} = '' OR COALESCE(ds.slotcode, '') ILIKE '%' || {slotF} || '%' OR COALESCE(ds.slotname, '') ILIKE '%' || {slotF} || '%')
  AND ({itemF} = '' OR i.itemcode ILIKE '%' || {itemF} || '%')
")
                .ToListAsync(ct);
            foreach (var row in ids)
                all.Add(row.Salesorderlineid);
        }

        return all.OrderBy(x => x).ToList();
    }

    /// <summary>
    /// 子品目の保管場所: stock と warehouses を warehousecode 昇順で最初の1件（複数倉庫時の既定）。
    /// </summary>
    public async Task<string> GetPrimaryWarehouseNameForItemAsync(string childItemcode, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(childItemcode))
            return "";
        var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
        var opened = conn.State != ConnectionState.Open;
        if (opened)
            await conn.OpenAsync(ct);
        try
        {
            await using var cmd = new NpgsqlCommand(
                """
                SELECT COALESCE(w.warehousename, w.warehousecode, '')
                FROM stock s
                INNER JOIN warehouses w ON w.warehousecode = s.warehousecode
                WHERE s.itemcode = @c
                ORDER BY w.warehousecode
                LIMIT 1
                """, conn);
            cmd.Parameters.AddWithValue("c", childItemcode);
            var o = await cmd.ExecuteScalarAsync(ct);
            return o?.ToString() ?? "";
        }
        finally
        {
            if (opened && conn.State == ConnectionState.Open)
                await conn.CloseAsync();
        }
    }

    public async Task<List<PreparationCsvRow>> BuildCsvRowsAsync(IReadOnlyList<long> lineIds, CancellationToken ct = default)
    {
        if (lineIds.Count == 0)
            return new List<PreparationCsvRow>();

        var headers = await FetchLineHeadersAsync(lineIds, ct);
        var bomCache = new Dictionary<string, List<PreparationBomSqlRow>>(StringComparer.Ordinal);
        var whCache = new Dictionary<string, string>(StringComparer.Ordinal);

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
                    WorkplaceName = h.WorkplaceNames,
                    DeliveryDate = h.NeedDate?.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture) ?? "",
                    Slot = h.SlotDisplay,
                    SmallClassName = h.MinorClassName,
                    OrderNo = h.Salesorderid.ToString(CultureInfo.InvariantCulture),
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
                if (!whCache.TryGetValue(b.ChildItemcode, out var wh))
                {
                    wh = await GetPrimaryWarehouseNameForItemAsync(b.ChildItemcode, ct);
                    whCache[b.ChildItemcode] = wh;
                }

                var qty = PreparationBomQuantity.ComputeRequiredQty(h.MfgQty, b.InputQty, b.OutputQty, b.YieldPercent);
                var qtyDisplay = qty.ToString("0.###", CultureInfo.InvariantCulture);
                rows.Add(new PreparationCsvRow
                {
                    WorkplaceName = h.WorkplaceNames,
                    DeliveryDate = h.NeedDate?.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture) ?? "",
                    Slot = h.SlotDisplay,
                    SmallClassName = h.MinorClassName,
                    OrderNo = h.Salesorderid.ToString(CultureInfo.InvariantCulture),
                    ParentItemcode = h.ParentItemcode,
                    ParentItemname = h.ParentItemname,
                    ChildItemcode = b.ChildItemcode,
                    ChildItemname = b.ChildItemname ?? "",
                    Quantity = qtyDisplay,
                    Unit = b.ChildUnitname ?? ""
                });
            }
        }

        return rows;
    }

    public async Task<List<PreparationPdfLineModel>> BuildPdfLineModelsAsync(IReadOnlyList<long> lineIds, CancellationToken ct = default)
    {
        if (lineIds.Count == 0)
            return new List<PreparationPdfLineModel>();

        var headers = await FetchLineHeadersAsync(lineIds, ct);
        var bomCache = new Dictionary<string, List<PreparationBomSqlRow>>(StringComparer.Ordinal);
        var whCache = new Dictionary<string, string>(StringComparer.Ordinal);

        var lines = new List<PreparationPdfLineModel>();
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
                lines.Add(new PreparationPdfLineModel
                {
                    MiddleClassificationName = h.MiddleClassName,
                    DateDisplay = h.NeedDate?.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture) ?? "",
                    OrderNo = h.Salesorderid.ToString(CultureInfo.InvariantCulture),
                    ParentItemcode = h.ParentItemcode,
                    ParentItemname = h.ParentItemname,
                    ChildItemcode = "",
                    ChildItemname = "",
                    Standard = "",
                    Quantity = "",
                    Unit = "",
                    WarehouseName = ""
                });
                continue;
            }

            foreach (var b in boms)
            {
                if (!whCache.TryGetValue(b.ChildItemcode, out var wh))
                {
                    wh = await GetPrimaryWarehouseNameForItemAsync(b.ChildItemcode, ct);
                    whCache[b.ChildItemcode] = wh;
                }

                var qty = PreparationBomQuantity.ComputeRequiredQty(h.MfgQty, b.InputQty, b.OutputQty, b.YieldPercent);
                var qtyDisplay = qty.ToString("0.###", CultureInfo.InvariantCulture);
                lines.Add(new PreparationPdfLineModel
                {
                    MiddleClassificationName = h.MiddleClassName,
                    DateDisplay = h.NeedDate?.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture) ?? "",
                    OrderNo = h.Salesorderid.ToString(CultureInfo.InvariantCulture),
                    ParentItemcode = h.ParentItemcode,
                    ParentItemname = h.ParentItemname,
                    ChildItemcode = b.ChildItemcode,
                    ChildItemname = b.ChildItemname ?? "",
                    Standard = b.ChildStd ?? "",
                    Quantity = qtyDisplay,
                    Unit = b.ChildUnitname ?? "",
                    WarehouseName = wh
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
                  COALESCE(mn.minorclassificationname, '') AS minor_class_name,
                  COALESCE(mid.middleclassificationname, '') AS middle_class_name,
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
                  ) AS need_date
                FROM salesorderline sol
                INNER JOIN item i ON i.itemcode = sol.itemcode
                LEFT JOIN minorclassification mn ON mn.majorclassificationcode = i.majorclassficationcode
                  AND mn.middleclassificationcode = i.middleclassficationcode
                  AND mn.minorclassificationcode = i.minorclassficationcode
                LEFT JOIN middleclassification mid ON mid.majorclassificationcode = i.majorclassficationcode
                  AND mid.middleclassificationcode = i.middleclassficationcode
                LEFT JOIN deliveryslot ds ON ds.slotcode = sol.slotcode
                WHERE sol.salesorderlineid = ANY(@ids)
                ORDER BY sol.salesorderlineid
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
                    Salesorderlineid = reader.GetInt64(0),
                    Salesorderid = reader.GetInt64(1),
                    MfgQty = reader.GetDecimal(2),
                    ParentItemcode = reader.GetString(3),
                    ParentItemname = reader.GetString(4),
                    MinorClassName = reader.GetString(5),
                    MiddleClassName = reader.GetString(6),
                    SlotDisplay = reader.GetString(7),
                    WorkplaceNames = reader.GetString(8),
                    PlannedDeliveryDate = ReadDateNullable(reader, 9),
                    NeedDate = ReadDateNullable(reader, 10)
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
                  COALESCE(
                    NULLIF(BTRIM(COALESCE(ia.std::text, '')), ''),
                    CASE WHEN ia.car1 IS NOT NULL AND ia.car1 > 0 THEN ia.car1::text END,
                    CASE WHEN ia.car2 IS NOT NULL AND ia.car2 > 0 THEN ia.car2::text END,
                    CASE WHEN ia.car3 IS NOT NULL AND ia.car3 > 0 THEN ia.car3::text END,
                    ''
                  ) AS child_std
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
    public string OrderNo { get; set; } = "";
    public string ParentItemcode { get; set; } = "";
    public string ParentItemname { get; set; } = "";
    public string ChildItemcode { get; set; } = "";
    public string ChildItemname { get; set; } = "";
    public string Standard { get; set; } = "";
    public string Quantity { get; set; } = "";
    public string Unit { get; set; } = "";
    public string WarehouseName { get; set; } = "";
}

internal sealed class PreparationLineHeaderRow
{
    public long Salesorderlineid { get; set; }
    public long Salesorderid { get; set; }
    public decimal MfgQty { get; set; }
    public string ParentItemcode { get; set; } = "";
    public string ParentItemname { get; set; } = "";
    public string MinorClassName { get; set; } = "";
    public string MiddleClassName { get; set; } = "";
    public string SlotDisplay { get; set; } = "";
    public string WorkplaceNames { get; set; } = "";
    public DateOnly? PlannedDeliveryDate { get; set; }
    /// <summary>納期（CSV/帳票に表示する日付）。ordertable.needdate 優先、無ければ planneddeliverydate。</summary>
    public DateOnly? NeedDate { get; set; }
}

internal sealed class PreparationBomSqlRow
{
    public string ChildItemcode { get; set; } = "";
    public decimal InputQty { get; set; }
    public decimal OutputQty { get; set; }
    public decimal YieldPercent { get; set; }
    public string? ChildItemname { get; set; }
    public string? ChildUnitname { get; set; }
    /// <summary>
    /// 子品目の規格表示: <c>itemadditionalinformation.std</c>（非空なら優先）。
    /// <c>std</c> が空のときは従来どおり <c>car1</c>→<c>car2</c>→<c>car3</c>の先頭の正値（マスタ未整備で <c>std</c> だけ空のケース向け）。
    /// </summary>
    public string? ChildStd { get; set; }
}
