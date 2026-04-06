using System.Globalization;
using ClosedXML.Excel;
using BaggingInstructions.Api.DTOs;

namespace BaggingInstructions.Api.Services;

/// <summary>
/// 仕分け照会・仕訳表自動調整の Excel。テンプレート不要で ClosedXML から生成する。
/// 仕分け照会: 1 行目得意先コード、2 納入場所コード、3 納入場所名、4〜6 保留、7 列見出し（品目・適用・得意先名）、8 行目以降は受注数量。
/// 仕訳表自動調整: 1 納入場所コード、2 納入場所名、3 列別最大収容（Σ 単位0÷addinfo01）、4 列見出し（店舗列は空）、5 行目以降は品目行で比（Σ 単位0÷addinfo01）。
/// </summary>
public sealed class SortingInquiryExcelService
{
    private const int ColItemCode = 1;
    private const int ColItemName = 2;
    private const int ColTekiyo = 3;
    private const int FirstCustomerCol = 4;
    private const int FrozenColumns = 3;
    private const string TekiyoColumnTitle = "適用";
    private const int StackedHeaderReservedBlankRows = 3;

    private const double MinWidthItemCode = 16;
    private const double MinWidthItemName = 40;
    private const double MinWidthTekiyo = 18;

    public byte[] BuildShiwakeInquiryWorkbook(SortingInquirySearchResponseDto data, string delvedtYyyymmdd)
    {
        _ = delvedtYyyymmdd;
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("仕分け照会");
        FillShiwakeWorksheet(ws, data);
        return SaveWorkbook(wb);
    }

    public byte[] BuildJournalAdjustmentWorkbook(SortingInquirySearchResponseDto data, string delvedtYyyymmdd)
    {
        _ = delvedtYyyymmdd;
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("仕訳表自動調整");
        FillJournalAdjustmentWorksheet(ws, data);
        return SaveWorkbook(wb);
    }

    private static byte[] SaveWorkbook(XLWorkbook wb)
    {
        using var stream = new MemoryStream();
        wb.SaveAs(stream);
        return stream.ToArray();
    }

    private static void FillShiwakeWorksheet(IXLWorksheet ws, SortingInquirySearchResponseDto data)
    {
        var n = data.StoreKeys.Count;
        var totalCol = n == 0 ? ColTekiyo + 1 : ColTekiyo + n + 1;

        var row = 1;
        for (var i = 0; i < n; i++)
        {
            var key = data.StoreKeys[i];
            var codeLabel = data.StoreHeaderCodes.TryGetValue(key, out var c) && !string.IsNullOrEmpty(c)
                ? c
                : key;
            ws.Cell(row, FirstCustomerCol + i).Value = codeLabel;
        }

        row++;
        for (var i = 0; i < n; i++)
        {
            var key = data.StoreKeys[i];
            if (data.StoreHeaderDeliveryCodes.TryGetValue(key, out var dc) && !string.IsNullOrEmpty(dc))
                ws.Cell(row, FirstCustomerCol + i).Value = dc;
        }

        row++;
        for (var i = 0; i < n; i++)
        {
            var key = data.StoreKeys[i];
            if (data.StoreHeaderDeliveryNames.TryGetValue(key, out var dn) && !string.IsNullOrEmpty(dn))
                ws.Cell(row, FirstCustomerCol + i).Value = dn;
        }

        row++;
        row += StackedHeaderReservedBlankRows;

        var headerRow = row;

        ws.Cell(headerRow, ColItemCode).Value = "品目コード";
        ws.Cell(headerRow, ColItemName).Value = "品目名称";
        ws.Cell(headerRow, ColTekiyo).Value = TekiyoColumnTitle;

        for (var i = 0; i < n; i++)
        {
            var key = data.StoreKeys[i];
            var header = data.StoreHeaders.TryGetValue(key, out var h) ? h : key;
            ws.Cell(headerRow, FirstCustomerCol + i).Value = header;
        }

        ws.Cell(headerRow, totalCol).Value = "合計";

        row++;
        WriteDataRows(ws, data, ref row, totalCol, n, useRatioQuantities: false);

        ws.Columns().AdjustToContents(1, 80);
        EnsureMinColumnWidth(ws.Column(ColItemCode), MinWidthItemCode);
        EnsureMinColumnWidth(ws.Column(ColItemName), MinWidthItemName);
        EnsureMinColumnWidth(ws.Column(ColTekiyo), MinWidthTekiyo);
        ws.SheetView.FreezeRows(headerRow);
        ws.SheetView.FreezeColumns(FrozenColumns);
        ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
    }

    private static void FillJournalAdjustmentWorksheet(IXLWorksheet ws, SortingInquirySearchResponseDto data)
    {
        var n = data.StoreKeys.Count;
        var totalCol = n == 0 ? ColTekiyo + 1 : ColTekiyo + n + 1;

        var row = 1;
        for (var i = 0; i < n; i++)
        {
            var key = data.StoreKeys[i];
            if (data.StoreHeaderDeliveryCodes.TryGetValue(key, out var dc) && !string.IsNullOrEmpty(dc))
                ws.Cell(row, FirstCustomerCol + i).Value = dc;
        }

        row++;
        for (var i = 0; i < n; i++)
        {
            var key = data.StoreKeys[i];
            if (data.StoreHeaderDeliveryNames.TryGetValue(key, out var dn) && !string.IsNullOrEmpty(dn))
                ws.Cell(row, FirstCustomerCol + i).Value = dn;
        }

        row++;
        for (var i = 0; i < n; i++)
        {
            var key = data.StoreKeys[i];
            if (data.StoreHeaderCapacities.TryGetValue(key, out var cap) && cap != 0)
            {
                var cell = ws.Cell(row, FirstCustomerCol + i);
                cell.Value = cap;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            }
        }

        row++;
        var headerRow = row;

        ws.Cell(headerRow, ColItemCode).Value = "品目コード";
        ws.Cell(headerRow, ColItemName).Value = "品目名称";
        ws.Cell(headerRow, ColTekiyo).Value = TekiyoColumnTitle;
        for (var i = 0; i < n; i++)
            ws.Cell(headerRow, FirstCustomerCol + i).Value = "";

        ws.Cell(headerRow, totalCol).Value = "合計";

        row++;
        WriteDataRows(ws, data, ref row, totalCol, n, useRatioQuantities: true);

        ws.Columns().AdjustToContents(1, 80);
        EnsureMinColumnWidth(ws.Column(ColItemCode), MinWidthItemCode);
        EnsureMinColumnWidth(ws.Column(ColItemName), MinWidthItemName);
        EnsureMinColumnWidth(ws.Column(ColTekiyo), MinWidthTekiyo);
        ws.SheetView.FreezeRows(headerRow);
        ws.SheetView.FreezeColumns(FrozenColumns);
        ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
    }

    private static void WriteDataRows(
        IXLWorksheet ws,
        SortingInquirySearchResponseDto data,
        ref int row,
        int totalCol,
        int n,
        bool useRatioQuantities)
    {
        var r = row;
        var lastStoreCol = n > 0 ? FirstCustomerCol + n - 1 : FirstCustomerCol;

        foreach (var line in data.Rows)
        {
            SetCellValueSmart(ws.Cell(r, ColItemCode), line.ItemCode);
            ws.Cell(r, ColItemName).Value = line.ItemName;
            ws.Cell(r, ColTekiyo).Value = line.FoodType;

            for (var i = 0; i < n; i++)
            {
                var key = data.StoreKeys[i];
                var c = FirstCustomerCol + i;
                decimal q;
                if (useRatioQuantities)
                {
                    if (!line.RatioQuantitiesByStore.TryGetValue(key, out q) || q == 0)
                        continue;
                }
                else
                {
                    if (!line.QuantitiesByStore.TryGetValue(key, out q) || q == 0)
                        continue;
                }

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

        row = r;
    }

    private static void EnsureMinColumnWidth(IXLColumn column, double minWidth)
    {
        if (column.Width < minWidth)
            column.Width = minWidth;
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
