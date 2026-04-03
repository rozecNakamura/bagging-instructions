using BaggingInstructions.Api.DTOs;
using BaggingInstructions.Api.Services;
using ClosedXML.Excel;

namespace BaggingInstructions.Api.Tests;

public class SortingInquiryExcelServiceTests
{
    private readonly SortingInquiryExcelService _svc = new();

    [Fact]
    public void BuildShiwakeInquiryWorkbook_matches_sample_layout()
    {
        var data = new SortingInquirySearchResponseDto
        {
            StoreKeys = new List<string> { "C|L1" },
            StoreHeaders = new Dictionary<string, string> { ["C|L1"] = "店A" },
            Rows = new List<SortingInquirySearchRowDto>
            {
                new()
                {
                    ItemCode = "I1",
                    ItemName = "品目1",
                    FoodType = "食種A",
                    QuantitiesByStore = new Dictionary<string, decimal> { ["C|L1"] = 3 }
                }
            }
        };

        var bytes = _svc.BuildShiwakeInquiryWorkbook(data, "20250710");
        Assert.NotEmpty(bytes);

        using var ms = new MemoryStream(bytes);
        using var wb = new XLWorkbook(ms);
        var ws = wb.Worksheet("仕分け照会");
        Assert.Contains("仕分け照会", ws.Cell(1, 1).GetString());
        Assert.Contains("2025-07-10", ws.Cell(1, 1).GetString());
        Assert.Equal("L1", ws.Cell(3, 5).GetString());
        Assert.Equal("検体", ws.Cell(4, 4).GetString());
        Assert.Equal("店A", ws.Cell(4, 5).GetString());
        // Reference template keeps 21 store slots; 合計 stays in column Z (26) when n < 21
        Assert.Equal("合計", ws.Cell(4, 26).GetString());
        Assert.Equal("I1", ws.Cell(8, 1).GetString());
        Assert.Equal("品目1", ws.Cell(8, 2).GetString());
        Assert.Equal("食種A", ws.Cell(8, 3).GetString());
        Assert.Equal(1, ws.Cell(8, 4).GetDouble());
        Assert.Equal(3, ws.Cell(8, 5).GetDouble());
    }

    [Fact]
    public void BuildJournalAdjustmentWorkbook_has_title_row()
    {
        var data = new SortingInquirySearchResponseDto
        {
            StoreKeys = new List<string>(),
            StoreHeaders = new Dictionary<string, string>(),
            Rows = new List<SortingInquirySearchRowDto>()
        };
        var bytes = _svc.BuildJournalAdjustmentWorkbook(data, "20250710");
        using var ms = new MemoryStream(bytes);
        using var wb = new XLWorkbook(ms);
        var ws = wb.Worksheet("仕訳表自動調整");
        Assert.Contains("仕訳表自動調整", ws.Cell(1, 1).GetString());
    }
}
