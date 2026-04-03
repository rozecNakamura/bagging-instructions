using BaggingInstructions.Api.DTOs;
using BaggingInstructions.Api.Services;
using ClosedXML.Excel;

namespace BaggingInstructions.Api.Tests;

public class SortingInquiryExcelServiceTests
{
    private readonly SortingInquiryExcelService _svc = new();

    [Fact]
    public void BuildShiwakeInquiryWorkbook_builds_grid_layout_without_template()
    {
        var data = new SortingInquirySearchResponseDto
        {
            StoreKeys = new List<string> { "CUSTX" },
            StoreHeaders = new Dictionary<string, string> { ["CUSTX"] = "店A" },
            Rows = new List<SortingInquirySearchRowDto>
            {
                new()
                {
                    ItemCode = "I1",
                    ItemName = "品目1",
                    FoodType = "食種A",
                    QuantitiesByStore = new Dictionary<string, decimal> { ["CUSTX"] = 3 }
                }
            }
        };

        var bytes = _svc.BuildShiwakeInquiryWorkbook(data, "20250710");
        Assert.NotEmpty(bytes);

        using var ms = new MemoryStream(bytes);
        using var wb = new XLWorkbook(ms);
        var ws = wb.Worksheet("仕分け照会");
        Assert.Equal("品目コード", ws.Cell(1, 1).GetString());
        Assert.Equal("店A", ws.Cell(1, 4).GetString());
        Assert.Equal("合計", ws.Cell(1, 5).GetString());
        Assert.Equal("I1", ws.Cell(2, 1).GetString());
        Assert.Equal("品目1", ws.Cell(2, 2).GetString());
        Assert.Equal("食種A", ws.Cell(2, 3).GetString());
        Assert.Equal(3, ws.Cell(2, 4).GetDouble());
    }

    [Fact]
    public void BuildJournalAdjustmentWorkbook_has_location_code_row_and_no_title()
    {
        var data = new SortingInquirySearchResponseDto
        {
            StoreKeys = new List<string> { "CUSTX" },
            StoreHeaders = new Dictionary<string, string> { ["CUSTX"] = "店A" },
            StoreHeaderCodes = new Dictionary<string, string> { ["CUSTX"] = "L001" },
            Rows = new List<SortingInquirySearchRowDto>
            {
                new()
                {
                    ItemCode = "I1",
                    ItemName = "品目1",
                    FoodType = "食種A",
                    QuantitiesByStore = new Dictionary<string, decimal> { ["CUSTX"] = 3 }
                }
            }
        };
        var bytes = _svc.BuildJournalAdjustmentWorkbook(data, "20250710");
        using var ms = new MemoryStream(bytes);
        using var wb = new XLWorkbook(ms);
        var ws = wb.Worksheet("仕訳表自動調整");
        Assert.DoesNotContain("喫食日", ws.Cell(1, 1).GetString());
        Assert.Equal("L001", ws.Cell(1, 4).GetString());
        Assert.Equal("品目コード", ws.Cell(2, 1).GetString());
        Assert.Equal("店A", ws.Cell(2, 4).GetString());
        Assert.Equal("合計", ws.Cell(2, 5).GetString());
        Assert.Equal("I1", ws.Cell(3, 1).GetString());
        Assert.Equal(3, ws.Cell(3, 4).GetDouble());
    }
}
