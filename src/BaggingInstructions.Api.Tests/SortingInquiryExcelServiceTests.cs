using BaggingInstructions.Api.DTOs;
using BaggingInstructions.Api.Services;
using ClosedXML.Excel;

namespace BaggingInstructions.Api.Tests;

public class SortingInquiryExcelServiceTests
{
    private readonly SortingInquiryExcelService _svc = new();

    [Fact]
    public void BuildShiwakeInquiryWorkbook_uses_same_stacked_headers_as_journal()
    {
        var data = new SortingInquirySearchResponseDto
        {
            StoreKeys = new List<string> { "CUSTX" },
            StoreHeaders = new Dictionary<string, string> { ["CUSTX"] = "店A" },
            StoreHeaderCodes = new Dictionary<string, string> { ["CUSTX"] = "CUSTX" },
            StoreHeaderDeliveryCodes = new Dictionary<string, string> { ["CUSTX"] = "WH01" },
            StoreHeaderDeliveryNames = new Dictionary<string, string> { ["CUSTX"] = "第1配送先" },
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
        Assert.Equal("CUSTX", ws.Cell(1, 4).GetString());
        Assert.Equal("WH01", ws.Cell(2, 4).GetString());
        Assert.Equal("第1配送先", ws.Cell(3, 4).GetString());
        Assert.Equal("品目コード", ws.Cell(7, 1).GetString());
        Assert.Equal("適用", ws.Cell(7, 3).GetString());
        Assert.Equal("店A", ws.Cell(7, 4).GetString());
        Assert.Equal("合計", ws.Cell(7, 5).GetString());
        Assert.Equal("I1", ws.Cell(8, 1).GetString());
        Assert.Equal("品目1", ws.Cell(8, 2).GetString());
        Assert.Equal("食種A", ws.Cell(8, 3).GetString());
        Assert.Equal(3, ws.Cell(8, 4).GetDouble());
    }

    [Fact]
    public void BuildJournalAdjustmentWorkbook_uses_delivery_stack_capacity_and_ratio_cells()
    {
        var data = new SortingInquirySearchResponseDto
        {
            StoreKeys = new List<string> { "CUSTX" },
            StoreHeaders = new Dictionary<string, string> { ["CUSTX"] = "店A" },
            StoreHeaderDeliveryCodes = new Dictionary<string, string> { ["CUSTX"] = "WH01" },
            StoreHeaderDeliveryNames = new Dictionary<string, string> { ["CUSTX"] = "第1配送先" },
            StoreHeaderCapacities = new Dictionary<string, decimal> { ["CUSTX"] = 7.5m },
            Rows = new List<SortingInquirySearchRowDto>
            {
                new()
                {
                    ItemCode = "I1",
                    ItemName = "品目1",
                    FoodType = "食種A",
                    QuantitiesByStore = new Dictionary<string, decimal> { ["CUSTX"] = 3 },
                    RatioQuantitiesByStore = new Dictionary<string, decimal> { ["CUSTX"] = 1.25m }
                }
            }
        };
        var bytes = _svc.BuildJournalAdjustmentWorkbook(data, "20250710");
        using var ms = new MemoryStream(bytes);
        using var wb = new XLWorkbook(ms);
        var ws = wb.Worksheet("仕訳表自動調整");
        Assert.DoesNotContain("喫食日", ws.Cell(1, 1).GetString());
        Assert.Equal("WH01", ws.Cell(1, 4).GetString());
        Assert.Equal("第1配送先", ws.Cell(2, 4).GetString());
        Assert.Equal(7.5, ws.Cell(3, 4).GetDouble());
        Assert.Equal("品目コード", ws.Cell(4, 1).GetString());
        Assert.Equal("適用", ws.Cell(4, 3).GetString());
        Assert.Equal("合計", ws.Cell(4, 5).GetString());
        Assert.Equal("I1", ws.Cell(5, 1).GetString());
        Assert.Equal("食種A", ws.Cell(5, 3).GetString());
        Assert.Equal(1.25, ws.Cell(5, 4).GetDouble());
    }
}
