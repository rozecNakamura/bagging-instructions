using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using BaggingInstructions.Api.Core;
using BaggingInstructions.Api.DTOs;
using BaggingInstructions.Api.Entities;

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
    /// 納入場所マスタ一覧（マルチセレクト用）。customercode, locationcode 昇順。
    /// </summary>
    public async Task<List<AcceptanceRecordDeliveryLocationOptionDto>> ListDeliveryLocationOptionsAsync(
        CancellationToken ct = default)
    {
        var locs = await _db.CustomerDeliveryLocations.AsNoTracking()
            .Include(l => l.Customer)
            .OrderBy(l => l.CustomerCode)
            .ThenBy(l => l.LocationCode)
            .ToListAsync(ct);
        return locs.Select(l => new AcceptanceRecordDeliveryLocationOptionDto
        {
            CustomerCode = l.CustomerCode ?? "",
            LocationCode = l.LocationCode ?? "",
            DisplayLabel = BuildDeliveryLocationLabel(l.LocationName, l.LocationCode, l.Customer)
        }).ToList();
    }

    private static string BuildDeliveryLocationLabel(string? locationName, string? locationCode, Customer? customer)
    {
        var nm = (locationName ?? "").Trim();
        var cd = (locationCode ?? "").Trim();
        var locPart = string.IsNullOrEmpty(nm) ? cd : (string.IsNullOrEmpty(cd) ? nm : $"{nm} ({cd})");
        var custNm = (customer?.CustomerShortName ?? customer?.CustomerName ?? "").Trim();
        var custCd = (customer?.CustomerCode ?? "").Trim();
        var custPart = string.IsNullOrEmpty(custNm) ? custCd : (string.IsNullOrEmpty(custCd) ? custNm : $"{custNm} ({custCd})");
        if (string.IsNullOrEmpty(custPart)) return string.IsNullOrEmpty(locPart) ? cd : locPart;
        return $"{locPart} / {custPart}";
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

        return rows
            .OrderBy(r => r.ItemName, StringComparer.Ordinal)
            .ThenBy(r => r.SalesOrderLineId)
            .Select(MapSearchRow)
            .ToList();
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
                  sol.salesorderlineid,
                  TO_CHAR(sol.planneddeliverydate, 'YYYYMMDD'),
                  COALESCE(ds.slotname, ds.slotcode, ''),
                  COALESCE(i.itemcode, ''),
                  COALESCE(i.itemname, ''),
                  COALESCE(u0.unitname, ''),
                  sol.quantity,
                  COALESCE(a.addinfo02, '')
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
                ORDER BY sol.salesorderlineid
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
                list.Add(new AcceptanceRecordSearchSqlRow
                {
                    SalesOrderLineId = reader.GetInt64(0),
                    DeliveryYyyymmdd = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    SlotDisplay = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    ItemCode = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    ItemName = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    UnitName = reader.IsDBNull(5) ? "" : reader.GetString(5),
                    LineQuantity = reader.IsDBNull(6) ? 0m : reader.GetDecimal(6),
                    Addinfo02 = reader.IsDBNull(7) ? "" : reader.GetString(7),
                    TotalQty = reader.IsDBNull(6) ? 0m : reader.GetDecimal(6)
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
                  COALESCE(ds.slotname, ds.slotcode, '') AS slot_display,
                  COALESCE(i.itemcode, '') AS itemcode,
                  COALESCE(i.itemname, '') AS itemname,
                  COALESCE(u0.unitname, '') AS unitname,
                  sol.quantity AS line_qty,
                  COALESCE(a.addinfo02, '') AS addinfo02
                FROM salesorderline sol
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
                var itemCode = reader.GetString(3);
                var itemName = reader.GetString(4);
                var lineQty = reader.GetDecimal(6);
                var addinfo02 = reader.IsDBNull(7) ? "" : reader.GetString(7);

                var childText = string.IsNullOrEmpty(itemCode)
                    ? itemName
                    : $"{itemCode} {itemName}".Trim();

                list.Add(new AcceptanceRecordPdfLineModel
                {
                    SalesOrderLineId = lineId,
                    EatDateDisplay = needDate?.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture) ?? "",
                    SlotDisplay = reader.GetString(2),
                    ChildItemText = childText,
                    MealCountDisplay = BentoPdfService.FormatMealCountDisplay(lineQty, addinfo02),
                    TotalQtyDisplay = lineQty.ToString("0.###", CultureInfo.InvariantCulture),
                    UnitName = reader.GetString(5)
                });
            }

            var order = new Dictionary<long, int>();
            for (var i = 0; i < lineIds.Count; i++)
                order[lineIds[i]] = i;

            return list
                .OrderBy(m => order.GetValueOrDefault(m.SalesOrderLineId, int.MaxValue))
                .ToList();
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

        return new AcceptanceRecordSearchRowDto
        {
            SalesOrderLineId = r.SalesOrderLineId,
            EatDate = eatDate,
            MealTime = r.SlotDisplay ?? "",
            ChildItem = childItem,
            MealCountDisplay = BentoPdfService.FormatMealCountDisplay(r.LineQuantity, r.Addinfo02),
            TotalQtyDisplay = r.TotalQty.ToString("0.###", CultureInfo.InvariantCulture),
            UnitName = r.UnitName ?? ""
        };
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
    public long SalesOrderLineId { get; set; }
    public string DeliveryYyyymmdd { get; set; } = "";
    public string? SlotDisplay { get; set; }
    public string? ItemCode { get; set; }
    public string? ItemName { get; set; }
    public string? UnitName { get; set; }
    /// <summary>salesorderline.quantity。食数・総量の両方に使用。</summary>
    public decimal LineQuantity { get; set; }
    public string? Addinfo02 { get; set; }
    /// <summary>表示用総量。LineQuantity と同じ salesorderline.quantity。</summary>
    public decimal TotalQty { get; set; }
}
