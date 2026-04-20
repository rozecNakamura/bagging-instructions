using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using BaggingInstructions.Api.Core;
using BaggingInstructions.Api.DTOs;

namespace BaggingInstructions.Api.Services;

/// <summary>
/// 検収の記録簿：salesorderline 基準の検索・PDF 行生成。数量は salesorderline.quantity。ordertable は使用しない。
/// </summary>
public sealed class AcceptanceRecordService
{
    private readonly AppDbContext _db;

    public AcceptanceRecordService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// 納入場所マスタ一覧（マルチセレクト用）。<c>customerdeliverylocation</c> を customercode, locationcode 昇順で返す。
    /// 表示は <c>locationcode：locationname</c>（いずれか欠ける場合は欠けない側のみ）。
    /// </summary>
    public async Task<List<AcceptanceRecordDeliveryLocationOptionDto>> ListDeliveryLocationOptionsAsync(
        CancellationToken ct = default)
    {
        var locs = await _db.CustomerDeliveryLocations.AsNoTracking()
            .OrderBy(l => l.CustomerCode)
            .ThenBy(l => l.LocationCode)
            .ToListAsync(ct);
        return locs.Select(l => new AcceptanceRecordDeliveryLocationOptionDto
        {
            CustomerCode = l.CustomerCode ?? "",
            LocationCode = l.LocationCode ?? "",
            DisplayLabel = BuildDeliveryLocationLabel(l.LocationCode, l.LocationName)
        }).ToList();
    }

    /// <summary>一覧表示：<c>locationcode：locationname</c>（全角コロン）。</summary>
    private static string BuildDeliveryLocationLabel(string? locationCode, string? locationName)
    {
        var cd = (locationCode ?? "").Trim();
        var nm = (locationName ?? "").Trim();
        if (string.IsNullOrEmpty(cd) && string.IsNullOrEmpty(nm)) return "";
        if (string.IsNullOrEmpty(nm)) return cd;
        if (string.IsNullOrEmpty(cd)) return nm;
        return $"{cd}：{nm}";
    }

    /// <summary>
    /// 納品日必須。出荷日・店舗（納入場所）は任意。1 行 = 1 salesorderline。ordertable は参照しない。
    /// storePairs は「customerCode + TAB + locationCode」。空のときは店舗で絞り込まない。
    /// </summary>
    public async Task<List<AcceptanceRecordSearchRowDto>> SearchAsync(
        string deliveryDate,
        string? shipDate,
        IReadOnlyList<string>? storePairs,
        CancellationToken ct = default)
    {
        var deliveryD = ParseYyyymmdd(deliveryDate);
        if (!deliveryD.HasValue)
            throw new ArgumentException("納品日はYYYYMMDD形式（8桁）で指定してください。", nameof(deliveryDate));

        var shipStr = NormalizeOptionalYyyymmdd(shipDate);
        var (storeCust, storeLoc) = ParseStorePairs(storePairs);
        var filterByStore = storeCust.Length > 0;

        var rows = await ExecuteSearchSqlAsync(deliveryD.Value, shipStr, filterByStore, storeCust, storeLoc, ct);

        return rows.Select(MapSearchRow).ToList();
    }

    private static (string[] Cust, string[] Loc) ParseStorePairs(IReadOnlyList<string>? storePairs)
    {
        var cust = new List<string>();
        var loc = new List<string>();
        foreach (var raw in storePairs ?? Array.Empty<string>())
        {
            var t = (raw ?? "").Trim();
            if (t.Length == 0) continue;
            var idx = t.IndexOf('\t');
            if (idx < 0)
            {
                cust.Add("");
                loc.Add(t);
                continue;
            }

            cust.Add(t[..idx].Trim());
            loc.Add(t[(idx + 1)..].Trim());
        }

        return (cust.ToArray(), loc.ToArray());
    }

    private async Task<List<AcceptanceRecordSearchSqlRow>> ExecuteSearchSqlAsync(
        DateOnly deliveryDate,
        string shipStr,
        bool filterByStore,
        string[] storeCust,
        string[] storeLoc,
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
                  ARRAY_AGG(sol.salesorderlineid ORDER BY sol.salesorderlineid),
                  TO_CHAR(MAX(sol.planneddeliverydate), 'YYYYMMDD'),
                  COALESCE(MAX(TRIM(COALESCE(cdl.locationname, ''))), ''),
                  COALESCE(MAX(TRIM(COALESCE(ds.slotname, COALESCE(ds.slotcode, '')))), ''),
                  COALESCE(TRIM(MAX(i.itemcode)), ''),
                  COALESCE(TRIM(MAX(i.itemname)), ''),
                  COALESCE(TRIM(MAX(u0.unitname)), ''),
                  SUM(sol.quantity),
                  MIN(COALESCE(a.addinfo02, ''))
                FROM salesorderline sol
                INNER JOIN salesorder so ON so.salesorderid = sol.salesorderid
                LEFT JOIN customerdeliverylocation cdl
                  ON cdl.locationcode IS NOT DISTINCT FROM so.customerdeliverylocationcode
                  AND cdl.customercode IS NOT DISTINCT FROM so.customercode
                INNER JOIN item i ON i.itemcode = sol.itemcode
                LEFT JOIN deliveryslot ds ON ds.slotcode = sol.slotcode
                LEFT JOIN salesorderlineaddinfo a ON a.salesorderlineid = sol.salesorderlineid
                LEFT JOIN unit u0 ON u0.unitcode = i.unitcode0
                WHERE sol.planneddeliverydate = @deliveryDate
                  AND (@shipStr = '' OR TO_CHAR(sol.plannedshipdate, 'YYYYMMDD') = @shipStr)
                  AND (
                    NOT @filterByStore
                    OR EXISTS (
                      SELECT 1
                      FROM unnest(@storeCust::text[], @storeLoc::text[]) AS t(cc, lc)
                      WHERE cdl.customercode IS NOT DISTINCT FROM t.cc
                        AND cdl.locationcode IS NOT DISTINCT FROM t.lc
                    )
                  )
                GROUP BY
                  sol.plannedshipdate,
                  sol.planneddeliverydate,
                  so.customercode,
                  so.customerdeliverylocationcode,
                  TRIM(COALESCE(ds.slotcode, '')),
                  TRIM(COALESCE(i.itemcode, ''))
                ORDER BY
                  MIN(TRIM(COALESCE(cdl.locationname, ''))),
                  MIN(sol.plannedshipdate),
                  MIN(sol.planneddeliverydate),
                  MIN(TRIM(COALESCE(ds.slotname, COALESCE(ds.slotcode, '')))),
                  MIN(TRIM(COALESCE(i.itemcode, '')))
                """, conn);

            cmd.Parameters.AddWithValue("deliveryDate", deliveryDate);
            cmd.Parameters.AddWithValue("shipStr", shipStr);
            cmd.Parameters.AddWithValue("filterByStore", filterByStore);
            cmd.Parameters.Add(new NpgsqlParameter("storeCust", NpgsqlDbType.Array | NpgsqlDbType.Text)
            {
                Value = filterByStore && storeCust.Length > 0 ? storeCust : Array.Empty<string>()
            });
            cmd.Parameters.Add(new NpgsqlParameter("storeLoc", NpgsqlDbType.Array | NpgsqlDbType.Text)
            {
                Value = filterByStore && storeLoc.Length > 0 ? storeLoc : Array.Empty<string>()
            });

            var list = new List<AcceptanceRecordSearchSqlRow>();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var lineIds = ReadInt64Array(reader, 0);
                list.Add(new AcceptanceRecordSearchSqlRow
                {
                    LineIds = lineIds,
                    DeliveryYyyymmdd = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    LocationName = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    SlotDisplay = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    ItemCode = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    ItemName = reader.IsDBNull(5) ? "" : reader.GetString(5),
                    UnitName = reader.IsDBNull(6) ? "" : reader.GetString(6),
                    LineQuantity = reader.IsDBNull(7) ? 0m : reader.GetDecimal(7),
                    Addinfo02 = reader.IsDBNull(8) ? "" : reader.GetString(8)
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

    /// <summary>PDF 用。lineIds は salesorderlineid。ordertable は参照しない。</summary>
    public async Task<List<AcceptanceRecordPdfLineModel>> BuildPdfLineModelsAsync(
        IReadOnlyList<long> lineIds,
        CancellationToken ct = default)
    {
        if (lineIds == null || lineIds.Count == 0)
            return new List<AcceptanceRecordPdfLineModel>();

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
                  sol.planneddeliverydate AS need_date,
                  sol.plannedshipdate,
                  COALESCE(cdl.locationname, '') AS location_name,
                  COALESCE(ds.slotname, ds.slotcode, '') AS slot_display,
                  COALESCE(i.itemcode, '') AS itemcode,
                  COALESCE(i.itemname, '') AS itemname,
                  COALESCE(u0.unitname, '') AS unitname,
                  sol.quantity AS line_qty,
                  COALESCE(a.addinfo02, '') AS addinfo02
                FROM salesorderline sol
                INNER JOIN salesorder so ON so.salesorderid = sol.salesorderid
                LEFT JOIN customerdeliverylocation cdl
                  ON cdl.locationcode IS NOT DISTINCT FROM so.customerdeliverylocationcode
                  AND cdl.customercode IS NOT DISTINCT FROM so.customercode
                INNER JOIN item i ON i.itemcode = sol.itemcode
                LEFT JOIN deliveryslot ds ON ds.slotcode = sol.slotcode
                LEFT JOIN salesorderlineaddinfo a ON a.salesorderlineid = sol.salesorderlineid
                LEFT JOIN unit u0 ON u0.unitcode = i.unitcode0
                WHERE sol.salesorderlineid = ANY(@ids)
                ORDER BY sol.salesorderlineid
                """, conn);
            cmd.Parameters.Add(new NpgsqlParameter("ids", NpgsqlDbType.Bigint | NpgsqlDbType.Array)
            {
                Value = lineIds.ToArray()
            });

            var list = new List<AcceptanceRecordPdfLineModel>();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var lineId = reader.GetInt64(0);
                var needDate = ReadDateNullable(reader, 1);
                var shipDate = ReadDateNullable(reader, 2);
                var locationName = reader.IsDBNull(3) ? "" : reader.GetString(3);
                var itemCode = reader.IsDBNull(5) ? "" : reader.GetString(5);
                var itemName = reader.IsDBNull(6) ? "" : reader.GetString(6);
                var lineQty = reader.GetDecimal(8);
                var addinfo02 = reader.IsDBNull(9) ? "" : reader.GetString(9);

                var childText = string.IsNullOrEmpty(itemCode)
                    ? itemName
                    : $"{itemCode} {itemName}".Trim();

                list.Add(new AcceptanceRecordPdfLineModel
                {
                    SalesOrderLineId = lineId,
                    ItemCode = itemCode.Trim(),
                    ItemName = itemName.Trim(),
                    DeliveryLocationName = locationName.Trim(),
                    PlannedShipDate = shipDate,
                    PlannedDeliveryDate = needDate,
                    EatDateDisplay = needDate?.ToString("MM/dd", CultureInfo.InvariantCulture) ?? "",
                    SlotDisplay = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    ChildItemText = childText,
                    MealCountDisplay = BentoPdfService.FormatMealCountDisplay(lineQty, addinfo02),
                    TotalQtyDisplay = lineQty.ToString("0.###", CultureInfo.InvariantCulture),
                    UnitName = reader.GetString(7),
                    LineQuantity = lineQty,
                    Addinfo02 = addinfo02
                });
            }

            return AggregatePdfLines(list);
        }
        finally
        {
            if (shouldClose && conn.State == ConnectionState.Open)
                await conn.CloseAsync();
        }
    }

    private static AcceptanceRecordSearchRowDto MapSearchRow(AcceptanceRecordSearchSqlRow r)
    {
        var eatDate = FormatDateDisplay(r.DeliveryYyyymmdd);
        var childItem = string.IsNullOrEmpty(r.ItemCode)
            ? r.ItemName ?? ""
            : $"{r.ItemCode} {r.ItemName}".Trim();

        var ids = r.LineIds ?? Array.Empty<long>();
        return new AcceptanceRecordSearchRowDto
        {
            SalesOrderLineIds = ids.ToList(),
            SalesOrderLineId = ids.Length > 0 ? ids[0] : 0,
            EatDate = eatDate,
            MealTime = r.SlotDisplay ?? "",
            ChildItem = childItem,
            MealCountDisplay = BentoPdfService.FormatMealCountDisplay(r.LineQuantity, r.Addinfo02 ?? ""),
            TotalQtyDisplay = r.LineQuantity.ToString("0.###", CultureInfo.InvariantCulture),
            UnitName = r.UnitName ?? ""
        };
    }

    /// <summary>
    /// 印刷用: 同一（納入場所・出荷日・納品日・喫食時間・品目）で数量を合算。
    /// </summary>
    private static List<AcceptanceRecordPdfLineModel> AggregatePdfLines(List<AcceptanceRecordPdfLineModel> raw)
    {
        return raw
            .GroupBy(m => (
                Fac: m.DeliveryLocationName ?? "",
                Ship: m.PlannedShipDate,
                Del: m.PlannedDeliveryDate,
                Slot: m.SlotDisplay ?? "",
                Item: m.ItemCode ?? ""
            ))
            .Select(g =>
            {
                var rows = g.OrderBy(x => x.SalesOrderLineId).ToList();
                var qty = rows.Sum(x => x.LineQuantity);
                var addinfo = rows.Select(x => x.Addinfo02).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)) ?? "";
                var head = rows[0];

                return new AcceptanceRecordPdfLineModel
                {
                    SalesOrderLineId = rows.Min(x => x.SalesOrderLineId),
                    ItemCode = head.ItemCode ?? "",
                    ItemName = head.ItemName ?? "",
                    DeliveryLocationName = head.DeliveryLocationName ?? "",
                    PlannedShipDate = head.PlannedShipDate,
                    PlannedDeliveryDate = head.PlannedDeliveryDate,
                    EatDateDisplay = head.EatDateDisplay,
                    SlotDisplay = head.SlotDisplay ?? "",
                    ChildItemText = head.ChildItemText ?? "",
                    MealCountDisplay = BentoPdfService.FormatMealCountDisplay(qty, addinfo),
                    TotalQtyDisplay = qty.ToString("0.###", CultureInfo.InvariantCulture),
                    UnitName = head.UnitName ?? "",
                    LineQuantity = qty,
                    Addinfo02 = addinfo
                };
            })
            .OrderBy(x => x.DeliveryLocationName, StringComparer.Ordinal)
            .ThenBy(x => x.PlannedShipDate ?? DateOnly.MaxValue)
            .ThenBy(x => x.PlannedDeliveryDate ?? DateOnly.MaxValue)
            .ThenBy(x => x.SlotDisplay ?? "", StringComparer.Ordinal)
            .ThenBy(x => x.ItemCode ?? "", StringComparer.Ordinal)
            .ThenBy(x => x.SalesOrderLineId)
            .ToList();
    }

    private static long[] ReadInt64Array(NpgsqlDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal)) return Array.Empty<long>();
        var v = reader.GetValue(ordinal);
        if (v is long[] la) return la;
        if (v is object[] oa)
        {
            var r = new long[oa.Length];
            for (var i = 0; i < oa.Length; i++)
                r[i] = Convert.ToInt64(oa[i], CultureInfo.InvariantCulture);
            return r;
        }

        return Array.Empty<long>();
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

    private static string NormalizeOptionalYyyymmdd(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var t = s.Trim();
        if (t.Length == 10 && t[4] == '-' && t[7] == '-')
            t = $"{t[..4]}{t[5..7]}{t[8..10]}";
        return t.Length == 8 ? t : "";
    }

    private static string FormatDateDisplay(string yyyymmdd)
    {
        if (string.IsNullOrEmpty(yyyymmdd) || yyyymmdd.Length != 8) return yyyymmdd;
        return $"{yyyymmdd[..4]}-{yyyymmdd.Substring(4, 2)}-{yyyymmdd.Substring(6, 2)}";
    }
}

internal sealed class AcceptanceRecordSearchSqlRow
{
    public long[] LineIds { get; set; } = Array.Empty<long>();
    public string DeliveryYyyymmdd { get; set; } = "";
    public string? LocationName { get; set; }
    public string? SlotDisplay { get; set; }
    public string? ItemCode { get; set; }
    public string? ItemName { get; set; }
    public string? UnitName { get; set; }
    public decimal LineQuantity { get; set; }
    public string? Addinfo02 { get; set; }
}
