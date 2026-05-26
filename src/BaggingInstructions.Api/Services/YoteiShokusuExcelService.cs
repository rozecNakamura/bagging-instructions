using ClosedXML.Excel;
using BaggingInstructions.Api.DTOs;

namespace BaggingInstructions.Api.Services;

/// <summary>
/// 予定食数 Excel 帳票（5_予定食数.xlsx 形式）を ClosedXML で生成する。
/// 列レイアウト（両グループ共通）:
///   A=店舗コード, B=店舗名称, C=基本食種（ヘッダ空白）, D=小計, E+=集計食種列..., 末尾=備考
/// グループ1（得意先200・210）: 店舗ごとに2行（上段=通常品 / 下段=検食・検体）
/// グループ2（得意先240・300・310）: 店舗ごとに1行
/// 各グループ末尾に合計行。
/// </summary>
public sealed class YoteiShokusuExcelService
{
    public byte[] BuildWorkbook(YoteiShokusuResponseDto data, string delvedtYyyymmdd, string mealTimeLabel = "")
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("予定食数");
        FillWorksheet(ws, data, delvedtYyyymmdd, mealTimeLabel);
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static void FillWorksheet(IXLWorksheet ws, YoteiShokusuResponseDto data, string delvedt, string mealTimeLabel)
    {
        var row = 1;

        // ─── 日付タイトル ───────────────────────────────────────────
        var timeSuffix = string.IsNullOrEmpty(mealTimeLabel) ? "" : $"　{mealTimeLabel}";
        ws.Cell(row, 1).Value = $"予定食数　{FormatDate(delvedt)}{timeSuffix}";
        ws.Cell(row, 1).Style.Font.Bold = true;
        row += 2;

        // ─── グループ1 ─────────────────────────────────────────────
        if (data.Group1Stores.Count > 0 || data.Group1Columns.Count > 0)
        {
            row = WriteGroup(ws, row, data.Group1Stores, data.Group1Columns, group1: true);
            row++;  // 空白行
        }

        // ─── グループ2 ─────────────────────────────────────────────
        if (data.Group2Stores.Count > 0 || data.Group2Columns.Count > 0)
        {
            WriteGroup(ws, row, data.Group2Stores, data.Group2Columns, group1: false);
        }

        ws.Columns().AdjustToContents(1, 50);
    }

    // ─── 共通グループ出力 ────────────────────────────────────────────
    // 列: A=店舗コード, B=店舗名称, C=基本食種, D=小計, E+=集計食種..., last=備考

    private static int WriteGroup(
        IXLWorksheet ws,
        int startRow,
        List<YoteiShokusuStoreDto> stores,
        List<string> cols,
        bool group1)
    {
        const int colLocCode = 1;
        const int colLocName = 2;
        const int colKihon = 3;
        const int colSubtotal = 4;
        const int firstSumCol = 5;
        var colRemarks = firstSumCol + cols.Count;

        var row = startRow;

        // ヘッダ行
        ws.Cell(row, colLocCode).Value = "店舗コード";
        ws.Cell(row, colLocName).Value = "店舗名称";
        // col C ヘッダは空白（テンプレートに合わせる）
        ws.Cell(row, colSubtotal).Value = "小計";
        for (var i = 0; i < cols.Count; i++)
            ws.Cell(row, firstSumCol + i).Value = cols[i];
        ws.Cell(row, colRemarks).Value = "備考";
        StyleHeaderRow(ws.Row(row), colRemarks);
        row++;

        var dataStartRow = row;

        // データ行
        foreach (var store in stores)
        {
            var storeStartRow = row;
            var isFirst = true;

            foreach (var r in store.Rows)
            {
                var subtotal = r.Quantities.Values.Sum();
                if (subtotal != 0)
                    ws.Cell(row, colSubtotal).Value = subtotal;

                for (var i = 0; i < cols.Count; i++)
                {
                    var qty = r.Quantities.TryGetValue(cols[i], out var q) ? q : 0m;
                    if (qty != 0m)
                        ws.Cell(row, firstSumCol + i).Value = qty;
                }

                if (isFirst)
                {
                    ws.Cell(row, colLocCode).Value = store.LocationCode;
                    ws.Cell(row, colLocName).Value = store.LocationName;
                    if (!string.IsNullOrEmpty(store.KihonShokushu))
                        ws.Cell(row, colKihon).Value = store.KihonShokushu;
                    if (!string.IsNullOrEmpty(store.Remarks))
                        ws.Cell(row, colRemarks).Value = store.Remarks;
                    isFirst = false;
                }
                row++;
            }

            // 店舗コード・店舗名を縦結合（2行以上のとき）
            if (row - storeStartRow > 1)
            {
                MergeVertical(ws, storeStartRow, row - 1, colLocCode);
                MergeVertical(ws, storeStartRow, row - 1, colLocName);
            }
        }

        // 合計行
        var dataEndRow = row - 1;
        if (dataEndRow >= dataStartRow)
        {
            ws.Cell(row, colLocCode).Value = "合計";
            ws.Cell(row, colLocCode).Style.Font.Bold = true;

            var totalSubtotal = stores.SelectMany(s => s.Rows).Sum(r => r.Quantities.Values.Sum());
            if (totalSubtotal != 0m)
                ws.Cell(row, colSubtotal).Value = totalSubtotal;

            for (var i = 0; i < cols.Count; i++)
            {
                var col = cols[i];
                var total = stores.SelectMany(s => s.Rows).Sum(r => r.Quantities.TryGetValue(col, out var q) ? q : 0m);
                if (total != 0m)
                    ws.Cell(row, firstSumCol + i).Value = total;
            }
            StyleHeaderRow(ws.Row(row), colRemarks);
            row++;
        }

        return row;
    }

    // ─── ヘルパー ─────────────────────────────────────────────────

    private static void MergeVertical(IXLWorksheet ws, int startRow, int endRow, int col)
    {
        var range = ws.Range(startRow, col, endRow, col);
        range.Merge();
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
    }

    private static void StyleHeaderRow(IXLRow row, int lastCol)
    {
        var range = row.Worksheet.Range(row.RowNumber(), 1, row.RowNumber(), lastCol);
        range.Style.Font.Bold = true;
        range.Style.Fill.BackgroundColor = XLColor.LightGray;
        range.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
    }

    private static string FormatDate(string yyyymmdd)
    {
        if (yyyymmdd.Length == 8 &&
            int.TryParse(yyyymmdd[..4], out var y) &&
            int.TryParse(yyyymmdd[4..6], out var m) &&
            int.TryParse(yyyymmdd[6..], out var d))
            return $"{y}年{m}月{d}日";
        return yyyymmdd;
    }
}
