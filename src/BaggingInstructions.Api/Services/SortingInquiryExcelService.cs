using System.Globalization;
using ClosedXML.Excel;
using BaggingInstructions.Api.DTOs;

namespace BaggingInstructions.Api.Services;

/// <summary>
/// 仕分け照会・仕訳表自動調整の Excel。ルートの 2_/3_ サンプル（テンプレート）のレイアウト・書式を流用し、
/// 行3納入場所コード、行4［検体・店舗名・合計］、行7縦計、行8～明細をデータで上書きする。
/// </summary>
public sealed class SortingInquiryExcelService
{
    private const int CodeRow = 3;
    private const int HeaderRow = 4;
    private const int HiddenRowStart = 5;
    private const int HiddenRowEnd = 6;
    private const int ColumnTotalRow = 7;
    private const int FirstDataRow = 8;
    private const int FrozenColumns = 3;
    private const int FrozenRows = 7;
    private const int FirstStoreCol = 5;
    private const int KentaiCol = 4;

    private const string ShiwakeTemplateFile = "shiwake-inquiry-template.xlsx";
    private const string JournalTemplateFile = "journal-adjustment-template.xlsx";

    public byte[] BuildShiwakeInquiryWorkbook(SortingInquirySearchResponseDto data, string delvedtYyyymmdd)
    {
        using var templateStream = OpenTemplate(ShiwakeTemplateFile);
        using var wb = new XLWorkbook(templateStream);
        var ws = wb.Worksheet(1);
        ws.Name = "仕分け照会";
        FillFromTemplate(ws, data, delvedtYyyymmdd, topLine: $"仕分け照会　喫食日: {FormatDateLabel(delvedtYyyymmdd)}");
        return SaveWorkbook(wb);
    }

    public byte[] BuildJournalAdjustmentWorkbook(SortingInquirySearchResponseDto data, string delvedtYyyymmdd)
    {
        using var templateStream = OpenTemplate(JournalTemplateFile);
        using var wb = new XLWorkbook(templateStream);
        var ws = wb.Worksheet(1);
        ws.Name = "仕訳表自動調整";
        FillFromTemplate(ws, data, delvedtYyyymmdd, topLine: $"仕訳表自動調整　喫食日: {FormatDateLabel(delvedtYyyymmdd)}");
        return SaveWorkbook(wb);
    }

    private static Stream OpenTemplate(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Templates", "SortingInquiry", fileName);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Sorting inquiry template not found: {path}", path);
        return File.OpenRead(path);
    }

    private static byte[] SaveWorkbook(XLWorkbook wb)
    {
        using var stream = new MemoryStream();
        wb.SaveAs(stream);
        return stream.ToArray();
    }

    private static void FillFromTemplate(
        IXLWorksheet ws,
        SortingInquirySearchResponseDto data,
        string delvedtYyyymmdd,
        string topLine)
    {
        _ = delvedtYyyymmdd;

        var totalCol = FindTotalColumn(ws, HeaderRow);
        var templateSlots = totalCol - FirstStoreCol;
        if (templateSlots < 1)
            throw new InvalidOperationException("Template has no store column region before 合計.");

        var n = data.StoreKeys.Count;

        if (n > templateSlots)
        {
            ws.Column(totalCol).InsertColumnsBefore(n - templateSlots);
            totalCol = FirstStoreCol + n;
        }

        var lastStoreCol = n > 0 ? FirstStoreCol + n - 1 : FirstStoreCol;

        ClearTemplateDataArea(ws, totalCol);

        ws.Cell(1, 1).Value = topLine;
        ws.Range(1, 1, 1, totalCol).Merge();

        if (n == 0)
            return;

        for (var i = 0; i < n; i++)
        {
            var key = data.StoreKeys[i];
            var locCode = LocationCodeFromStoreKey(key);
            var c = FirstStoreCol + i;
            SetCellValueSmart(ws.Cell(CodeRow, c), locCode);
        }

        ws.Cell(HeaderRow, KentaiCol).Value = "検体";
        for (var i = 0; i < n; i++)
        {
            var key = data.StoreKeys[i];
            var header = data.StoreHeaders.TryGetValue(key, out var h) ? h : key;
            ws.Cell(HeaderRow, FirstStoreCol + i).Value = header;
        }

        ws.Cell(HeaderRow, totalCol).Value = "合計";

        ws.Row(HiddenRowStart).Hide();
        ws.Row(HiddenRowEnd).Hide();

        var unusedStart = FirstStoreCol + n;
        if (unusedStart < totalCol)
        {
            var clearBottom = Math.Max(ws.LastRowUsed()?.RowNumber() ?? ColumnTotalRow, FirstDataRow + 64);
            ws.Range(CodeRow, unusedStart, clearBottom, totalCol - 1).Clear(XLClearOptions.Contents);
        }

        for (var c = FirstStoreCol; c <= FirstStoreCol + n - 1; c++)
        {
            ws.Cell(HiddenRowStart, c).Clear(XLClearOptions.Contents);
            ws.Cell(HiddenRowEnd, c).Clear(XLClearOptions.Contents);
        }

        var lastDataRow = FirstDataRow + Math.Max(data.Rows.Count - 1, 0);

        for (var i = 0; i < n; i++)
        {
            var c = FirstStoreCol + i;
            var colLet = ws.Cell(ColumnTotalRow, c).Address.ColumnLetter;
            ws.Cell(ColumnTotalRow, c).FormulaA1 =
                $"SUM({colLet}{FirstDataRow}:{colLet}{lastDataRow})";
        }

        if (unusedStart < totalCol)
            ws.Range(ColumnTotalRow, unusedStart, ColumnTotalRow, totalCol - 1).Clear(XLClearOptions.Contents);

        var firstLet = ws.Cell(ColumnTotalRow, FirstStoreCol).Address.ColumnLetter;
        var lastLet = ws.Cell(ColumnTotalRow, lastStoreCol).Address.ColumnLetter;
        ws.Cell(ColumnTotalRow, totalCol).FormulaA1 = $"SUM({firstLet}{ColumnTotalRow}:{lastLet}{ColumnTotalRow})";

        var r = FirstDataRow;
        foreach (var line in data.Rows)
        {
            SetCellValueSmart(ws.Cell(r, 1), line.ItemCode);
            ws.Cell(r, 2).Value = line.ItemName;
            ws.Cell(r, 3).Value = line.FoodType;
            ws.Cell(r, KentaiCol).Value = 1;
            for (var i = 0; i < n; i++)
            {
                var key = data.StoreKeys[i];
                var c = FirstStoreCol + i;
                if (line.QuantitiesByStore.TryGetValue(key, out var q) && q != 0)
                    ws.Cell(r, c).Value = q;
                else
                    ws.Cell(r, c).Clear(XLClearOptions.Contents);
            }

            if (unusedStart < totalCol)
                ws.Range(r, unusedStart, r, totalCol - 1).Clear(XLClearOptions.Contents);

            var rowFirstLet = ws.Cell(r, FirstStoreCol).Address.ColumnLetter;
            var rowLastLet = ws.Cell(r, lastStoreCol).Address.ColumnLetter;
            ws.Cell(r, totalCol).FormulaA1 = $"SUM({rowFirstLet}{r}:{rowLastLet}{r})";
            r++;
        }

        ws.SheetView.FreezeRows(FrozenRows);
        ws.SheetView.FreezeColumns(FrozenColumns);
        ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;

        ws.Range(HeaderRow, 1, HeaderRow, totalCol).Style.Font.Bold = true;
    }

    private static void ClearTemplateDataArea(IXLWorksheet ws, int totalCol)
    {
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? FirstDataRow;
        if (lastRow < FirstDataRow)
            return;

        ws.Range(FirstDataRow, 1, lastRow, totalCol).Clear(XLClearOptions.Contents);
    }

    private static int FindTotalColumn(IXLWorksheet ws, int headerRow)
    {
        var last = ws.LastColumnUsed()?.ColumnNumber() ?? FirstStoreCol + 21;
        for (var c = FirstStoreCol; c <= last; c++)
        {
            var s = ws.Cell(headerRow, c).GetString().Trim();
            if (s == "合計")
                return c;
        }

        throw new InvalidOperationException($"Template row {headerRow} must contain a 合計 column.");
    }

    private static string LocationCodeFromStoreKey(string storeKey)
    {
        var parts = storeKey.Split('|', 2, StringSplitOptions.None);
        return parts.Length >= 2 ? parts[1].Trim() : storeKey.Trim();
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

    private static string FormatDateLabel(string yyyymmdd)
    {
        if (yyyymmdd.Length == 8
            && DateOnly.TryParseExact(yyyymmdd, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return yyyymmdd;
    }
}
