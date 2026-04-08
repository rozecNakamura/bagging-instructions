using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using BaggingInstructions.Api.Core;
using BaggingInstructions.Api.DTOs;

namespace BaggingInstructions.Api.Services;

/// <summary>
/// 検品記録簿用の検索・PDF 行生成サービス。
/// ordertable（ordertype=PO、suppliercode、itemcode）→ supplier マスタ、item、itemadditionalinformation、unit を用いる。
/// PDF・検索の数量は単位0換算（<c>qtyuni0</c> 優先、無ければ <c>qtyuniN×item.conversionvalueN</c>、無ければ <c>qty</c> を ia.std1/std2/std3/car0 で換算）。
/// 規格表示は <c>itemadditionalinformation.std1→std2→std3</c> の先頭非空、単位名は <c>unit.unitname</c>（unitcode0）。
/// </summary>
public sealed class InspectionRecordService
{
    private readonly AppDbContext _db;

    public InspectionRecordService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// 仕入先マスタ一覧（マルチセレクト用）。suppliercode 昇順。
    /// </summary>
    public async Task<List<InspectionRecordSupplierOptionDto>> ListSupplierOptionsAsync(CancellationToken ct = default)
    {
        return await _db.Suppliers.AsNoTracking()
            .OrderBy(s => s.SupplierCode)
            .Select(s => new InspectionRecordSupplierOptionDto
            {
                SupplierCode = s.SupplierCode,
                SupplierName = s.SupplierName ?? ""
            })
            .ToListAsync(ct);
    }

    /// <summary>
    /// 納期・仕入先（任意の複数コード）でオーダー行を検索する。1 行 = 1 ordertable レコード。
    /// supplierCodes が null または空のときは仕入先で絞り込まない。
    /// </summary>
    public async Task<List<InspectionRecordSearchRowDto>> SearchAsync(
        string needDate,
        IReadOnlyList<string>? supplierCodes,
        CancellationToken ct = default)
    {
        var date = ParseYyyymmdd(needDate);
        if (!date.HasValue)
            throw new ArgumentException("納期はYYYYMMDD形式（8桁）で指定してください。", nameof(needDate));

        var codes = (supplierCodes ?? Array.Empty<string>())
            .Select(c => (c ?? "").Trim())
            .Where(c => c.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var filterBySupplier = codes.Length > 0;

        var rows = await ExecuteSearchSqlAsync(date.Value, filterBySupplier, codes, ct);

        return rows.Select(r =>
        {
            var needDateDisplay = FormatDateDisplay(r.NeedDate);
            var supplierDisplay = string.IsNullOrEmpty(r.SupplierCode)
                ? r.SupplierName ?? ""
                : $"{r.SupplierCode} {r.SupplierName}";

            var qtyDisplay = r.Qty.ToString("0.###", CultureInfo.InvariantCulture);

            return new InspectionRecordSearchRowDto
            {
                OrderTableId = r.OrderTableId,
                OrderNo = r.OrderNo ?? "",
                SupplierDisplay = supplierDisplay,
                NeedDate = needDateDisplay,
                ItemCode = r.ItemCode ?? "",
                ItemName = r.ItemName ?? "",
                QuantityDisplay = qtyDisplay,
                UnitName = r.UnitName ?? ""
            };
        }).ToList();
    }

    /// <summary>
    /// PDF 用の明細行を生成する。orderIds は ordertableid。
    /// </summary>
    public async Task<List<InspectionRecordPdfLineModel>> BuildPdfLineModelsAsync(
        IReadOnlyList<long> orderIds,
        CancellationToken ct = default)
    {
        if (orderIds == null || orderIds.Count == 0)
            return new List<InspectionRecordPdfLineModel>();

        var headers = await FetchLineHeadersAsync(orderIds, ct);
        if (headers.Count == 0)
            return new List<InspectionRecordPdfLineModel>();

        var lines = new List<InspectionRecordPdfLineModel>();
        foreach (var h in headers)
        {
            var needDateDisplay = h.NeedDate?.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture) ?? "";
            var qtyDisplay = h.Qty.ToString("0.###", CultureInfo.InvariantCulture);

            lines.Add(new InspectionRecordPdfLineModel
            {
                DeliveryDateDisplay = needDateDisplay,
                OrderNo = h.OrderNo ?? "",
                ItemCode = h.ItemCode ?? "",
                ItemName = h.ItemName ?? "",
                Spec = h.Spec ?? "",
                QuantityDisplay = qtyDisplay,
                UnitName = h.UnitName ?? "",
                DeviationHandling = "",
                StorageLocation = "",
                DeliveryTime = "",
                TemperatureCheck = "",
                BestBefore = "",
                FreshnessGrade = "",
                ExternalAppearance = ""
            });
        }

        return lines;
    }

    private async Task<List<InspectionRecordSearchSqlRow>> ExecuteSearchSqlAsync(
        DateOnly needDate,
        bool filterBySupplier,
        string[] supplierCodes,
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
                  ot.ordertableid::text,
                  COALESCE(ot.suppliercode, ''),
                  COALESCE(s.suppliername, ''),
                  TO_CHAR(ot.needdate, 'YYYYMMDD'),
                  COALESCE(i.itemcode, ''),
                  COALESCE(i.itemname, ''),
                  COALESCE(u0.unitname, ''),
                  COALESCE(ot.qty, 0),
                  ia.std1,
                  ia.std2,
                  ia.std3,
                  ia.car0,
                  ot.qtyuni0,
                  ot.qtyuni1,
                  ot.qtyuni2,
                  ot.qtyuni3,
                  i.conversionvalue1,
                  i.conversionvalue2,
                  i.conversionvalue3
                FROM ordertable ot
                INNER JOIN item i ON i.itemcode = NULLIF(TRIM(ot.itemcode), '')
                LEFT JOIN itemadditionalinformation ia ON ia.itemcode = i.itemcode
                LEFT JOIN unit u0 ON u0.unitcode = i.unitcode0
                LEFT JOIN supplier s ON s.suppliercode = ot.suppliercode
                WHERE UPPER(TRIM(COALESCE(ot.ordertype, ''))) = 'PO'
                  AND ot.needdate = @needDate
                  AND (NOT @filterBySupplier OR ot.suppliercode = ANY(@supplierCodes))
                ORDER BY ot.ordertableid
                """, conn);

            cmd.Parameters.AddWithValue("needDate", needDate);
            cmd.Parameters.AddWithValue("filterBySupplier", filterBySupplier);
            cmd.Parameters.Add(new NpgsqlParameter("supplierCodes", NpgsqlDbType.Array | NpgsqlDbType.Text)
            {
                Value = filterBySupplier && supplierCodes.Length > 0
                    ? supplierCodes
                    : Array.Empty<string>()
            });

            var list = new List<InspectionRecordSearchSqlRow>();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var qtyRaw = reader.IsDBNull(8) ? 0m : reader.GetDecimal(8);
                var iaStd1 = reader.IsDBNull(9) ? null : reader.GetString(9);
                var iaStd2 = reader.IsDBNull(10) ? null : reader.GetString(10);
                var iaStd3 = reader.IsDBNull(11) ? null : reader.GetString(11);
                decimal? iaCar0 = reader.IsDBNull(12) ? null : reader.GetDecimal(12);
                decimal? qtyUni0 = reader.IsDBNull(13) ? null : reader.GetDecimal(13);
                decimal? qtyUni1 = reader.IsDBNull(14) ? null : reader.GetDecimal(14);
                decimal? qtyUni2 = reader.IsDBNull(15) ? null : reader.GetDecimal(15);
                decimal? qtyUni3 = reader.IsDBNull(16) ? null : reader.GetDecimal(16);
                decimal? cv1 = reader.IsDBNull(17) ? null : reader.GetDecimal(17);
                decimal? cv2 = reader.IsDBNull(18) ? null : reader.GetDecimal(18);
                decimal? cv3 = reader.IsDBNull(19) ? null : reader.GetDecimal(19);
                var qtyU0 = CookingInstructionQuantity.ResolveParentQtyInUnit0(
                    qtyRaw, qtyUni0, qtyUni1, qtyUni2, qtyUni3, iaStd1, iaStd2, iaStd3, iaCar0, cv1, cv2, cv3);

                list.Add(new InspectionRecordSearchSqlRow
                {
                    OrderTableId = reader.GetInt64(0),
                    OrderNo = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    SupplierCode = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    SupplierName = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    NeedDate = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    ItemCode = reader.IsDBNull(5) ? "" : reader.GetString(5),
                    ItemName = reader.IsDBNull(6) ? "" : reader.GetString(6),
                    UnitName = reader.IsDBNull(7) ? "" : reader.GetString(7),
                    Qty = qtyU0
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

    private async Task<List<InspectionRecordHeaderRow>> FetchLineHeadersAsync(
        IReadOnlyList<long> orderIds,
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
                  ot.ordertableid::text AS order_no,
                  ot.needdate AS need_date,
                  COALESCE(i.itemcode, '') AS item_code,
                  COALESCE(i.itemname, '') AS item_name,
                  COALESCE(
                    NULLIF(TRIM(COALESCE(ia.std1, '')), ''),
                    NULLIF(TRIM(COALESCE(ia.std2, '')), ''),
                    NULLIF(TRIM(COALESCE(ia.std3, '')), ''),
                    ''
                  ) AS spec,
                  COALESCE(u0.unitname, '') AS unit_name,
                  COALESCE(ot.qty, 0) AS qty,
                  ia.std1,
                  ia.std2,
                  ia.std3,
                  ia.car0,
                  ot.qtyuni0,
                  ot.qtyuni1,
                  ot.qtyuni2,
                  ot.qtyuni3,
                  i.conversionvalue1,
                  i.conversionvalue2,
                  i.conversionvalue3
                FROM ordertable ot
                INNER JOIN item i ON i.itemcode = NULLIF(TRIM(ot.itemcode), '')
                LEFT JOIN itemadditionalinformation ia ON ia.itemcode = i.itemcode
                LEFT JOIN unit u0 ON u0.unitcode = i.unitcode0
                WHERE ot.ordertableid = ANY(@ids)
                  AND UPPER(TRIM(COALESCE(ot.ordertype, ''))) = 'PO'
                ORDER BY ot.ordertableid
                """, conn);
            cmd.Parameters.Add(new NpgsqlParameter("ids", NpgsqlDbType.Bigint | NpgsqlDbType.Array)
            {
                Value = orderIds.ToArray()
            });

            var list = new List<InspectionRecordHeaderRow>();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var qtyRaw = reader.GetDecimal(7);
                var iaStd1 = reader.IsDBNull(8) ? null : reader.GetString(8);
                var iaStd2 = reader.IsDBNull(9) ? null : reader.GetString(9);
                var iaStd3 = reader.IsDBNull(10) ? null : reader.GetString(10);
                decimal? iaCar0 = reader.IsDBNull(11) ? null : reader.GetDecimal(11);
                decimal? qtyUni0 = reader.IsDBNull(12) ? null : reader.GetDecimal(12);
                decimal? qtyUni1 = reader.IsDBNull(13) ? null : reader.GetDecimal(13);
                decimal? qtyUni2 = reader.IsDBNull(14) ? null : reader.GetDecimal(14);
                decimal? qtyUni3 = reader.IsDBNull(15) ? null : reader.GetDecimal(15);
                decimal? cv1 = reader.IsDBNull(16) ? null : reader.GetDecimal(16);
                decimal? cv2 = reader.IsDBNull(17) ? null : reader.GetDecimal(17);
                decimal? cv3 = reader.IsDBNull(18) ? null : reader.GetDecimal(18);
                var qtyU0 = CookingInstructionQuantity.ResolveParentQtyInUnit0(
                    qtyRaw, qtyUni0, qtyUni1, qtyUni2, qtyUni3, iaStd1, iaStd2, iaStd3, iaCar0, cv1, cv2, cv3);

                list.Add(new InspectionRecordHeaderRow
                {
                    OrderTableId = reader.GetInt64(0),
                    OrderNo = reader.GetString(1),
                    NeedDate = ReadDateNullable(reader, 2),
                    ItemCode = reader.GetString(3),
                    ItemName = reader.GetString(4),
                    Spec = reader.GetString(5),
                    UnitName = reader.GetString(6),
                    Qty = qtyU0
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

internal sealed class InspectionRecordSearchSqlRow
{
    public long OrderTableId { get; set; }
    public string? OrderNo { get; set; }
    public string? SupplierCode { get; set; }
    public string? SupplierName { get; set; }
    public string NeedDate { get; set; } = "";
    public string? ItemCode { get; set; }
    public string? ItemName { get; set; }
    public string? UnitName { get; set; }
    public decimal Qty { get; set; }
}

internal sealed class InspectionRecordHeaderRow
{
    public long OrderTableId { get; set; }
    public string OrderNo { get; set; } = "";
    public DateOnly? NeedDate { get; set; }
    public string ItemCode { get; set; } = "";
    public string ItemName { get; set; } = "";
    public string Spec { get; set; } = "";
    public string UnitName { get; set; } = "";
    public decimal Qty { get; set; }
}

