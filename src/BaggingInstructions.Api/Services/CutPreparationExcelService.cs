using System.Globalization;
using ClosedXML.Excel;

namespace BaggingInstructions.Api.Services;

/// <summary>
/// カット前準備書 Excel 出力（ClosedXML）。
/// レイアウトは 4_下処理係【LLC外販】.xls に準拠。
/// </summary>
public sealed class CutPreparationExcelService
{
    // 列定義
    private const int ColNo = 1;
    private const int ColDate = 2;
    private const int ColMfgRoute = 3;
    private const int ColClass = 4;
    private const int ColOrderNo = 5;  // 注番（ordertable.ordertableid）
    private const int ColParentCode = 6;
    private const int ColParentName = 7;
    private const int ColChildCode = 8;
    private const int ColChildName = 9;
    private const int ColQty = 10;
    private const int ColUnit = 11;
    private const int ColWarehouse = 12;

    private static readonly (int Col, double Width, string Header)[] Columns =
    {
        (ColNo,           6.9,  "No."),
        (ColDate,         8.6,  "日付"),
        (ColMfgRoute,     8.2,  "製造便"),
        (ColClass,        8.2,  "分類名"),
        (ColOrderNo,      12.4, "注番"),
        (ColParentCode,   12.4, "親品目ｺｰﾄﾞ"),
        (ColParentName,   28.6, "親品目名称"),
        (ColChildCode,    12.4, "子品目ｺｰﾄﾞ"),
        (ColChildName,    28.6, "子品目名称"),
        (ColQty,          11.4, "数量"),
        (ColUnit,          4.4, "単位"),
        (ColWarehouse,     8.2, "保管場所"),
    };

    public byte[] Build(IReadOnlyList<CutPreparationPdfLineModel> lines)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("カット前準備書");

        // 列幅・ヘッダー設定
        foreach (var (col, width, header) in Columns)
        {
            ws.Column(col).Width = width;
            var cell = ws.Cell(1, col);
            cell.Value = header;
            cell.Style.Font.Bold = true;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9E1F2");
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }

        // ヘッダー行の高さ
        ws.Row(1).Height = 18;

        // データ行
        for (var i = 0; i < lines.Count; i++)
        {
            var row = i + 2;
            var line = lines[i];

            ws.Cell(row, ColNo).Value = i + 1;
            ws.Cell(row, ColDate).Value = line.DateDisplay;
            ws.Cell(row, ColMfgRoute).Value = line.ManufacturingRoute;
            ws.Cell(row, ColClass).Value = line.MiddleClassName;
            if (long.TryParse(line.OrderNo, out var orderId))
                ws.Cell(row, ColOrderNo).Value = orderId;
            else
                ws.Cell(row, ColOrderNo).Value = line.OrderNo;
            ws.Cell(row, ColParentCode).Value = line.ParentItemcode;
            ws.Cell(row, ColParentName).Value = line.ParentItemname;
            ws.Cell(row, ColChildCode).Value = line.ChildItemcode;
            ws.Cell(row, ColChildName).Value = line.ChildItemname;

            // 数量：数値として設定できる場合は数値型で
            if (decimal.TryParse(line.Quantity, NumberStyles.Any, CultureInfo.InvariantCulture, out var qty))
                ws.Cell(row, ColQty).Value = qty;
            else
                ws.Cell(row, ColQty).Value = line.Quantity;

            ws.Cell(row, ColUnit).Value = line.Unit;
            ws.Cell(row, ColWarehouse).Value = line.WarehouseName;

            // 罫線
            var rowRange = ws.Range(row, ColNo, row, ColWarehouse);
            rowRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            rowRange.Style.Border.InsideBorder = XLBorderStyleValues.Hair;

            // 数量列は右詰め
            ws.Cell(row, ColQty).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ws.Cell(row, ColNo).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        // ヘッダー行に外枠
        ws.Range(1, ColNo, 1, ColWarehouse).Style.Border.OutsideBorder = XLBorderStyleValues.Medium;

        // ウィンドウ枠の固定（ヘッダー行）
        ws.SheetView.Freeze(1, 0);

        using var stream = new MemoryStream();
        wb.SaveAs(stream);
        return stream.ToArray();
    }
}
