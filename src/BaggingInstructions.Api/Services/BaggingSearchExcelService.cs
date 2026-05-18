using ClosedXML.Excel;
using BaggingInstructions.Api.DTOs;

namespace BaggingInstructions.Api.Services;

/// <summary>袋詰指示書・ラベル管理画面の検索結果を Excel 出力（ClosedXML）。</summary>
public sealed class BaggingSearchExcelService
{
    private const int ColNo = 1;
    private const int ColPrddt = 2;
    private const int ColItemcd = 3;
    private const int ColItemnm = 4;
    private const int ColQty = 5;
    private const int ColUnit = 6;
    private const int ColStatus = 7;

    private static readonly (int Col, double Width, string Header)[] Columns =
    {
        (ColNo,     5.5,  "No."),
        (ColPrddt,  12.0, "製造日"),
        (ColItemcd, 14.0, "品目コード"),
        (ColItemnm, 36.0, "品目名"),
        (ColQty,    12.0, "受注数量"),
        (ColUnit,    7.0, "単位"),
        (ColStatus,  9.0, "状態"),
    };

    public byte[] Build(IReadOnlyList<BaggingSearchGroupDto> groups, string? prddt)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("袋詰管理");

        // タイトル行
        var title = "袋詰指示書・ラベル管理";
        if (!string.IsNullOrEmpty(prddt) && prddt.Length == 8)
            title += $"　製造日: {prddt[..4]}/{prddt[4..6]}/{prddt[6..8]}";
        var titleCell = ws.Cell(1, ColNo);
        titleCell.Value = title;
        titleCell.Style.Font.Bold = true;
        titleCell.Style.Font.FontSize = 12;
        ws.Range(1, ColNo, 1, ColStatus).Merge();
        ws.Row(1).Height = 20;

        // ヘッダー行
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

        // データ行
        for (var i = 0; i < groups.Count; i++)
        {
            var row = i + 3;
            var g = groups[i];
            var prddtDisplay = g.Prddt.Length == 8
                ? $"{g.Prddt[..4]}/{g.Prddt[4..6]}/{g.Prddt[6..8]}"
                : g.Prddt;
            var statusLabel = g.IsPrinted ? "完了"
                : g.IsInstructionPrinted ? "指示書済み"
                : g.IsLabelPrinted ? "ラベル済み"
                : "未完了";

            ws.Cell(row, ColNo).Value = i + 1;
            ws.Cell(row, ColPrddt).Value = prddtDisplay;
            ws.Cell(row, ColItemcd).Value = g.Itemcd;
            ws.Cell(row, ColItemnm).Value = g.Itemnm ?? "";
            ws.Cell(row, ColQty).Value = g.TotalJobordqun;
            ws.Cell(row, ColUnit).Value = g.UnitName ?? g.UnitCode ?? "";
            ws.Cell(row, ColStatus).Value = statusLabel;

            var rowRange = ws.Range(row, ColNo, row, ColStatus);
            rowRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            rowRange.Style.Border.InsideBorder = XLBorderStyleValues.Hair;

            ws.Cell(row, ColNo).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(row, ColQty).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ws.Cell(row, ColStatus).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            if (g.IsPrinted)
                ws.Cell(row, ColStatus).Style.Fill.BackgroundColor = XLColor.FromHtml("#C6EFCE");
            else if (g.IsInstructionPrinted || g.IsLabelPrinted)
                ws.Cell(row, ColStatus).Style.Fill.BackgroundColor = XLColor.FromHtml("#FFEB9C");
        }

        if (groups.Count > 0)
            ws.Range(2, ColNo, groups.Count + 2, ColStatus).Style.Border.OutsideBorder = XLBorderStyleValues.Medium;

        ws.SheetView.Freeze(2, 0);

        using var stream = new MemoryStream();
        wb.SaveAs(stream);
        return stream.ToArray();
    }
}
