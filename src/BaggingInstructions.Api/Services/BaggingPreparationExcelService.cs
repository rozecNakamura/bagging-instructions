using System.Data;
using System.Globalization;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using BaggingInstructions.Api.Core;

namespace BaggingInstructions.Api.Services;

/// <summary>袋詰投入量登録画面から呼ぶ「作業前準備書貼付け」Excel 出力サービス。</summary>
public sealed class BaggingPreparationExcelService
{
    private readonly AppDbContext _db;

    private static readonly (int Col, double Width, string Header)[] Columns =
    {
        (1,  12.0, "職場名"),
        (2,  12.0, "日付"),
        (3,  12.0, "製造便"),
        (4,   9.0, "殺菌温度"),
        (5,  14.0, "注番"),
        (6,  14.0, "親品目コード"),
        (7,  30.0, "親品目"),
        (8,  14.0, "子品目コード"),
        (9,  30.0, "子品目"),
        (10,  9.0, "規格"),
        (11,  9.0, "数量"),
        (12,  7.0, "単位"),
        (13, 20.0, "保管場所"),
    };

    public BaggingPreparationExcelService(AppDbContext db) => _db = db;

    public async Task<byte[]> BuildAsync(IReadOnlyList<long> salesOrderLineIds, string prddt, bool aggregate = false, CancellationToken ct = default)
    {
        if (salesOrderLineIds.Count == 0)
            return BuildEmpty(prddt);

        var asOf = ParseYyyymmdd(prddt) ?? DateOnly.FromDateTime(DateTime.Today);
        var prddtDisplay = prddt.Length == 8
            ? $"{prddt[..4]}/{prddt[4..6]}/{prddt[6..8]}"
            : prddt;

        var headers = await FetchHeadersAsync(salesOrderLineIds, ct);
        var bomCache = new Dictionary<string, List<BaggingPrepBomRow>>(StringComparer.OrdinalIgnoreCase);
        var dataRows = new List<BaggingPrepExcelRow>();

        foreach (var h in headers)
        {
            if (!bomCache.TryGetValue(h.ParentItemcode, out var boms))
            {
                boms = await FetchBomsAsync(h.ParentItemcode, asOf, ct);
                bomCache[h.ParentItemcode] = boms;
            }

            if (boms.Count == 0)
            {
                dataRows.Add(new BaggingPrepExcelRow
                {
                    WorkplaceName = h.WorkplaceName,
                    DateDisplay = prddtDisplay,
                    MfgRouteName = h.MfgRouteName,
                    ParentSteriTempRange = h.ParentSteriTempRange,
                    OrderNo = h.SalesOrderLineId.ToString(CultureInfo.InvariantCulture),
                    ParentItemcode = h.ParentItemcode,
                    ParentItemname = h.ParentItemname,
                });
                continue;
            }

            foreach (var b in boms)
            {
                var qty = PreparationBomQuantity.ComputeRequiredQty(h.Quantity, b.InputQty, b.OutputQty, b.YieldPercent);
                dataRows.Add(new BaggingPrepExcelRow
                {
                    WorkplaceName = h.WorkplaceName,
                    DateDisplay = prddtDisplay,
                    MfgRouteName = h.MfgRouteName,
                    ParentSteriTempRange = h.ParentSteriTempRange,
                    OrderNo = h.SalesOrderLineId.ToString(CultureInfo.InvariantCulture),
                    ParentItemcode = h.ParentItemcode,
                    ParentItemname = h.ParentItemname,
                    ChildItemcode = b.ChildItemcode,
                    ChildItemname = b.ChildItemname ?? "",
                    ChildStd = b.ChildStd ?? "",
                    Quantity = ReportQuantityFormatter.FormatCeilingQuantity(qty),
                    ChildUnitname = b.ChildUnitname ?? "",
                    ChildWarehouseName = b.ChildWarehouseName ?? "",
                });
            }
        }

        if (aggregate)
        {
            dataRows = dataRows
                .GroupBy(r => (r.ParentItemcode, r.ChildItemcode))
                .Select(g =>
                {
                    var first = g.First();
                    var total = g.Sum(r => decimal.TryParse(r.Quantity, out var q) ? q : 0m);
                    return new BaggingPrepExcelRow
                    {
                        WorkplaceName = first.WorkplaceName,
                        DateDisplay = first.DateDisplay,
                        MfgRouteName = "",
                        ParentSteriTempRange = first.ParentSteriTempRange,
                        OrderNo = "",
                        ParentItemcode = first.ParentItemcode,
                        ParentItemname = first.ParentItemname,
                        ChildItemcode = first.ChildItemcode,
                        ChildItemname = first.ChildItemname,
                        ChildStd = first.ChildStd,
                        Quantity = total.ToString("0", CultureInfo.InvariantCulture),
                        ChildUnitname = first.ChildUnitname,
                        ChildWarehouseName = first.ChildWarehouseName,
                    };
                })
                .OrderBy(r => r.ParentItemcode, StringComparer.Ordinal)
                .ThenBy(r => r.ChildItemcode, StringComparer.Ordinal)
                .ToList();
        }

        return BuildWorkbook(dataRows, prddt);
    }

    private byte[] BuildEmpty(string prddt) => BuildWorkbook(new List<BaggingPrepExcelRow>(), prddt);

    private static byte[] BuildWorkbook(IReadOnlyList<BaggingPrepExcelRow> rows, string prddt)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("作業前準備書貼付け");

        var title = "作業前準備書貼付け";
        if (prddt.Length == 8)
            title += $"　製造日: {prddt[..4]}/{prddt[4..6]}/{prddt[6..8]}";
        int lastCol = Columns[^1].Col;
        var titleCell = ws.Cell(1, 1);
        titleCell.Value = title;
        titleCell.Style.Font.Bold = true;
        titleCell.Style.Font.FontSize = 12;
        ws.Range(1, 1, 1, lastCol).Merge();
        ws.Row(1).Height = 20;

        foreach (var (col, width, header) in Columns)
        {
            ws.Column(col).Width = width;
            var cell = ws.Cell(2, col);
            cell.Value = header;
            cell.Style.Font.Bold = true;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9E1F2");
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }
        ws.Row(2).Height = 18;

        for (var i = 0; i < rows.Count; i++)
        {
            var rowNo = i + 3;
            var r = rows[i];
            ws.Cell(rowNo, 1).Value = r.WorkplaceName;
            ws.Cell(rowNo, 2).Value = r.DateDisplay;
            ws.Cell(rowNo, 3).Value = r.MfgRouteName;
            ws.Cell(rowNo, 4).Value = r.ParentSteriTempRange;
            ws.Cell(rowNo, 5).Value = r.OrderNo;
            ws.Cell(rowNo, 6).Value = r.ParentItemcode;
            ws.Cell(rowNo, 7).Value = r.ParentItemname;
            ws.Cell(rowNo, 8).Value = r.ChildItemcode;
            ws.Cell(rowNo, 9).Value = r.ChildItemname;
            ws.Cell(rowNo, 10).Value = r.ChildStd;
            if (!string.IsNullOrEmpty(r.Quantity) && decimal.TryParse(r.Quantity, out var qDec))
                ws.Cell(rowNo, 11).Value = qDec;
            else
                ws.Cell(rowNo, 11).Value = r.Quantity;
            ws.Cell(rowNo, 12).Value = r.ChildUnitname;
            ws.Cell(rowNo, 13).Value = r.ChildWarehouseName;

            var range = ws.Range(rowNo, 1, rowNo, lastCol);
            range.Style.Border.OutsideBorder = XLBorderStyleValues.Hair;
            ws.Cell(rowNo, 11).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        }

        if (rows.Count > 0)
            ws.Range(2, 1, rows.Count + 2, lastCol).Style.Border.OutsideBorder = XLBorderStyleValues.Medium;

        ws.SheetView.Freeze(2, 0);

        using var stream = new MemoryStream();
        wb.SaveAs(stream);
        return stream.ToArray();
    }

    private async Task<List<BaggingPrepHeaderRow>> FetchHeadersAsync(IReadOnlyList<long> ids, CancellationToken ct)
    {
        var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
        var shouldClose = conn.State != ConnectionState.Open;
        if (shouldClose) await conn.OpenAsync(ct);
        try
        {
            await using var cmd = new NpgsqlCommand(
                """
                SELECT
                  sol.salesorderlineid,
                  COALESCE(sol.quantity, 0)                                   AS quantity,
                  TRIM(COALESCE(i.itemcode, sol.itemcode, ''))                AS parent_itemcode,
                  COALESCE(i.itemname, '')                                    AS parent_itemname,
                  COALESCE(wc.workcentername, '')                             AS workplace_name,
                  TRIM(COALESCE(sai.addinfo03name, ''))                       AS mfg_route_name,
                  CASE
                    WHEN ia.steritemprange IS NULL THEN ''
                    ELSE TO_CHAR(ia.steritemprange, 'FM999999990.###')
                  END                                                         AS parent_steri_temprange
                FROM salesorderline sol
                LEFT JOIN item i             ON TRIM(i.itemcode)          = TRIM(sol.itemcode)
                LEFT JOIN workcenter wc      ON TRIM(wc.workcentercode)   = TRIM(i.workcentercode)
                LEFT JOIN salesorderlineaddinfo sai ON sai.salesorderlineid = sol.salesorderlineid
                LEFT JOIN itemadditionalinformation ia ON TRIM(ia.itemcode) = TRIM(i.itemcode)
                WHERE sol.salesorderlineid = ANY(@ids)
                ORDER BY sol.salesorderlineid
                """, conn);
            cmd.Parameters.Add(new NpgsqlParameter("ids", NpgsqlDbType.Bigint | NpgsqlDbType.Array)
            {
                Value = ids.ToArray()
            });

            var list = new List<BaggingPrepHeaderRow>();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                list.Add(new BaggingPrepHeaderRow
                {
                    SalesOrderLineId = reader.GetInt64(0),
                    Quantity = reader.GetDecimal(1),
                    ParentItemcode = reader.GetString(2),
                    ParentItemname = reader.GetString(3),
                    WorkplaceName = reader.GetString(4),
                    MfgRouteName = reader.GetString(5),
                    ParentSteriTempRange = reader.GetString(6),
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

    private async Task<List<BaggingPrepBomRow>> FetchBomsAsync(string parentItemcode, DateOnly asOf, CancellationToken ct)
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
                  COALESCE(ci.itemname, '')                                          AS child_itemname,
                  COALESCE(u.unitname, '')                                           AS child_unitname,
                  COALESCE(BTRIM(COALESCE(ia.std::text, '')), '')                    AS child_std,
                  COALESCE(wh_child.warehousename, wh_child.warehousecode, '')       AS child_warehouse_name
                FROM bom b
                LEFT JOIN item ci              ON TRIM(ci.itemcode)        = TRIM(b.childitemcode)
                LEFT JOIN warehouses wh_child  ON wh_child.warehousecode   = ci.warehousecode
                LEFT JOIN unit u               ON u.unitcode               = ci.unitcode0
                LEFT JOIN itemadditionalinformation ia ON TRIM(ia.itemcode) = TRIM(b.childitemcode)
                WHERE b.parentitemcode = @p
                  AND b.childitemcode IS NOT NULL
                  AND (b.startdate IS NULL OR b.startdate <= @asof)
                  AND (b.enddate   IS NULL OR b.enddate   >= @asof)
                ORDER BY b.productionorder NULLS LAST, b.childitemcode
                """, conn);
            cmd.Parameters.AddWithValue("p", parentItemcode);
            cmd.Parameters.AddWithValue("asof", asOf);

            var list = new List<BaggingPrepBomRow>();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                list.Add(new BaggingPrepBomRow
                {
                    ChildItemcode = reader.GetString(0),
                    InputQty = reader.GetDecimal(1),
                    OutputQty = reader.GetDecimal(2),
                    YieldPercent = reader.GetDecimal(3),
                    ChildItemname = reader.GetString(4),
                    ChildUnitname = reader.GetString(5),
                    ChildStd = reader.GetString(6),
                    ChildWarehouseName = reader.GetString(7),
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
        if (int.TryParse(s.AsSpan(0, 4), out var y) &&
            int.TryParse(s.AsSpan(4, 2), out var m) &&
            int.TryParse(s.AsSpan(6, 2), out var d))
            return new DateOnly(y, m, d);
        return null;
    }

    private sealed class BaggingPrepHeaderRow
    {
        public long SalesOrderLineId { get; set; }
        public decimal Quantity { get; set; }
        public string ParentItemcode { get; set; } = "";
        public string ParentItemname { get; set; } = "";
        public string WorkplaceName { get; set; } = "";
        public string MfgRouteName { get; set; } = "";
        public string ParentSteriTempRange { get; set; } = "";
    }

    private sealed class BaggingPrepBomRow
    {
        public string ChildItemcode { get; set; } = "";
        public decimal InputQty { get; set; }
        public decimal OutputQty { get; set; }
        public decimal YieldPercent { get; set; }
        public string? ChildItemname { get; set; }
        public string? ChildUnitname { get; set; }
        public string? ChildStd { get; set; }
        public string? ChildWarehouseName { get; set; }
    }

    private sealed class BaggingPrepExcelRow
    {
        public string WorkplaceName { get; set; } = "";
        public string DateDisplay { get; set; } = "";
        public string MfgRouteName { get; set; } = "";
        public string ParentSteriTempRange { get; set; } = "";
        public string OrderNo { get; set; } = "";
        public string ParentItemcode { get; set; } = "";
        public string ParentItemname { get; set; } = "";
        public string ChildItemcode { get; set; } = "";
        public string ChildItemname { get; set; } = "";
        public string ChildStd { get; set; } = "";
        public string Quantity { get; set; } = "";
        public string ChildUnitname { get; set; } = "";
        public string ChildWarehouseName { get; set; } = "";
    }
}
