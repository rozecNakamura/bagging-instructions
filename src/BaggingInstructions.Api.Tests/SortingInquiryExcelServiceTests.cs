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
        Assert.Equal("品目コード", ws.Cell(4, 1).GetString());
        Assert.Equal("適用", ws.Cell(4, 3).GetString());
        Assert.Equal("店A", ws.Cell(4, 4).GetString());
        Assert.Equal("合計", ws.Cell(4, 5).GetString());
        Assert.Equal("I1", ws.Cell(5, 1).GetString());
        Assert.Equal("品目1", ws.Cell(5, 2).GetString());
        Assert.Equal("食種A", ws.Cell(5, 3).GetString());
        Assert.Equal(3, ws.Cell(5, 4).GetDouble());
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
            StoreHeaderCapacities = new Dictionary<string, decimal> { ["CUSTX"] = 1.25m },
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
        Assert.Equal("品目コード", ws.Cell(3, 1).GetString());
        Assert.Equal("品目名称", ws.Cell(3, 2).GetString());
        Assert.Equal("適用", ws.Cell(3, 3).GetString());
        Assert.Equal(1.25, ws.Cell(3, 4).GetDouble());
        Assert.Equal("合計", ws.Cell(3, 5).GetString());
        Assert.Equal("I1", ws.Cell(4, 1).GetString());
        Assert.Equal("品目1", ws.Cell(4, 2).GetString());
        Assert.Equal("食種A", ws.Cell(4, 3).GetString());
        Assert.Equal(1.25, ws.Cell(4, 4).GetDouble());
    }

    [Fact]
    public void BuildJournalAdjustmentWorkbook_row3_is_max_per_column_over_item_rows()
    {
        var data = new SortingInquirySearchResponseDto
        {
            StoreKeys = new List<string> { "A" },
            StoreHeaders = new Dictionary<string, string> { ["A"] = "店" },
            StoreHeaderDeliveryCodes = new Dictionary<string, string> { ["A"] = "WH" },
            StoreHeaderDeliveryNames = new Dictionary<string, string> { ["A"] = "納入" },
            StoreHeaderCapacities = new Dictionary<string, decimal> { ["A"] = 10m },
            Rows = new List<SortingInquirySearchRowDto>
            {
                new()
                {
                    ItemCode = "X",
                    ItemName = "x",
                    FoodType = "昼",
                    RatioQuantitiesByStore = new Dictionary<string, decimal> { ["A"] = 3m }
                },
                new()
                {
                    ItemCode = "Y",
                    ItemName = "y",
                    FoodType = "夜",
                    RatioQuantitiesByStore = new Dictionary<string, decimal> { ["A"] = 4m }
                }
            }
        };
        var bytes = _svc.BuildJournalAdjustmentWorkbook(data, "20250710");
        using var ms = new MemoryStream(bytes);
        using var wb = new XLWorkbook(ms);
        var ws = wb.Worksheet("仕訳表自動調整");
        Assert.Equal("品目コード", ws.Cell(3, 1).GetString());
        Assert.Equal(4, ws.Cell(3, 4).GetDouble());
        Assert.Equal("合計", ws.Cell(3, 5).GetString());
        Assert.Equal("X", ws.Cell(4, 1).GetString());
        Assert.Equal("Y", ws.Cell(5, 1).GetString());
    }

    [Fact]
    public void BuildJournalAdjustmentWorkbook_row3_is_zero_when_no_ratio_cells()
    {
        var data = new SortingInquirySearchResponseDto
        {
            StoreKeys = new List<string> { "A" },
            StoreHeaders = new Dictionary<string, string> { ["A"] = "店" },
            StoreHeaderDeliveryCodes = new Dictionary<string, string> { ["A"] = "WH" },
            StoreHeaderDeliveryNames = new Dictionary<string, string> { ["A"] = "納入" },
            StoreHeaderCapacities = new Dictionary<string, decimal> { ["A"] = 9m },
            Rows = new List<SortingInquirySearchRowDto>
            {
                new()
                {
                    ItemCode = "X",
                    ItemName = "x",
                    FoodType = "昼",
                    QuantitiesByStore = new Dictionary<string, decimal> { ["A"] = 100m },
                    RatioQuantitiesByStore = new Dictionary<string, decimal>()
                }
            }
        };
        var bytes = _svc.BuildJournalAdjustmentWorkbook(data, "20250710");
        using var ms = new MemoryStream(bytes);
        using var wb = new XLWorkbook(ms);
        var ws = wb.Worksheet("仕訳表自動調整");
        Assert.Equal("品目コード", ws.Cell(3, 1).GetString());
        Assert.Equal(0, ws.Cell(3, 4).GetDouble());
        Assert.Equal("合計", ws.Cell(3, 5).GetString());
    }
}
