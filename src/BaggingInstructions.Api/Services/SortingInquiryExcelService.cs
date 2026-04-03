using System.Globalization;
using ClosedXML.Excel;
using BaggingInstructions.Api.DTOs;

namespace BaggingInstructions.Api.Services;

/// <summary>
/// 仕分け照会・仕訳表自動調整の Excel。テンプレート不要で ClosedXML から生成し、検索一覧と同じ列（品目コード・品目名称・食種・納入場所…・合計）に揃える。
/// 仕訳表自動調整のみ、納入場所名称行の上に納入場所コード行を付与する。
/// </summary>
public sealed class SortingInquiryExcelService
{
    private const int ColItemCode = 1;
    private const int ColItemName = 2;
    private const int ColFoodType = 3;
    private const int FirstCustomerCol = 4;
    private const int FrozenColumns = 3;

    public byte[] BuildShiwakeInquiryWorkbook(SortingInquirySearchResponseDto data, string delvedtYyyymmdd)
    {
        _ = delvedtYyyymmdd;
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("仕分け照会");
        FillWorksheet(ws, data, title: null, storeCodeRow: false);
        return SaveWorkbook(wb);
    }

    public byte[] BuildJournalAdjustmentWorkbook(SortingInquirySearchResponseDto data, string delvedtYyyymmdd)
    {
        _ = delvedtYyyymmdd;
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("仕訳表自動調整");
        FillWorksheet(ws, data, title: null, storeCodeRow: true);
        return SaveWorkbook(wb);
    }

    private static byte[] SaveWorkbook(XLWorkbook wb)
    {
        using var stream = new MemoryStream();
        wb.SaveAs(stream);
        return stream.ToArray();
    }

    /// <param name="title">null のとき先頭タイトル行を付けない。</param>
    /// <param name="storeCodeRow">true のとき、納入場所名称行の直上に納入場所コード行を出す（仕訳表自動調整）。</param>
    private static void FillWorksheet(IXLWorksheet ws, SortingInquirySearchResponseDto data, string? title, bool storeCodeRow = false)
    {
        var n = data.StoreKeys.Count;
        var totalCol = n == 0 ? ColFoodType + 1 : ColFoodType + n + 1;

        var row = 1;
        if (!string.IsNullOrEmpty(title))
        {
            ws.Cell(row, 1).Value = title;
            ws.Range(row, 1, row, totalCol).Merge();
            ws.Row(row).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            row++;
        }

        if (storeCodeRow)
        {
            for (var i = 0; i < n; i++)
            {
                var key = data.StoreKeys[i];
                var codeLabel = data.StoreHeaderCodes.TryGetValue(key, out var c) && !string.IsNullOrEmpty(c)
                    ? c
                    : key;
                ws.Cell(row, FirstCustomerCol + i).Value = codeLabel;
            }

            ws.Range(row, FirstCustomerCol, row, n > 0 ? FirstCustomerCol + n - 1 : FirstCustomerCol)
                .Style.Font.Bold = true;
            row++;
        }

        var headerRow = row;
        var firstDataRow = headerRow + 1;
        var freezeRows = headerRow;

        ws.Cell(headerRow, ColItemCode).Value = "品目コード";
        ws.Cell(headerRow, ColItemName).Value = "品目名称";
        ws.Cell(headerRow, ColFoodType).Value = "食種";

        for (var i = 0; i < n; i++)
        {
            var key = data.StoreKeys[i];
            var header = data.StoreHeaders.TryGetValue(key, out var h) ? h : key;
            ws.Cell(headerRow, FirstCustomerCol + i).Value = header;
        }

        ws.Cell(headerRow, totalCol).Value = "合計";
        ws.Range(headerRow, 1, headerRow, totalCol).Style.Font.Bold = true;

        var r = firstDataRow;
        var lastStoreCol = n > 0 ? FirstCustomerCol + n - 1 : FirstCustomerCol;

        foreach (var line in data.Rows)
        {
            SetCellValueSmart(ws.Cell(r, ColItemCode), line.ItemCode);
            ws.Cell(r, ColItemName).Value = line.ItemName;
            ws.Cell(r, ColFoodType).Value = line.FoodType;

            for (var i = 0; i < n; i++)
            {
                var key = data.StoreKeys[i];
                var c = FirstCustomerCol + i;
                if (line.QuantitiesByStore.TryGetValue(key, out var q) && q != 0)
                    ws.Cell(r, c).Value = q;
            }

            if (n > 0)
            {
                var rowFirstLet = ws.Cell(r, FirstCustomerCol).Address.ColumnLetter;
                var rowLastLet = ws.Cell(r, lastStoreCol).Address.ColumnLetter;
                ws.Cell(r, totalCol).FormulaA1 = $"SUM({rowFirstLet}{r}:{rowLastLet}{r})";
            }
            else
                ws.Cell(r, totalCol).Value = 0;

            r++;
        }

        ws.Columns().AdjustToContents(1, 80);
        ws.SheetView.FreezeRows(freezeRows);
        ws.SheetView.FreezeColumns(FrozenColumns);
        ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
    }

    private static void SetCellValueSmart(IXLCell cell, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            cell.Value = "";
            return;
        }

        var t = value.Trim();
        if (decimal.TryParse(t, NumberStyles.Number, CultureInfo.InvariantCulture, out var dec))
            cell.Value = dec;
        else if (decimal.TryParse(t, NumberStyles.Number, CultureInfo.GetCultureInfo("ja-JP"), out var decJa))
            cell.Value = decJa;
        else
            cell.Value = t;
    }
}
