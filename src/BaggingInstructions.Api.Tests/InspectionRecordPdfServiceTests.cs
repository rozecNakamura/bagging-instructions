using BaggingInstructions.Api.DTOs;
using BaggingInstructions.Api.Services;

namespace BaggingInstructions.Api.Tests;

public class InspectionRecordPdfServiceTests
{
    [Fact]
    public void BuildPageTagValues_maps_rxz_merge_names_for_spec_qty_unit()
    {
        var line = new InspectionRecordPdfLineModel
        {
            DeliveryDateDisplay = "2025/03/01",
            OrderNo = "1001",
            ItemCode = "P001",
            ItemName = "親品目",
            Spec = "1/4",
            QuantityDisplay = "12.5",
            UnitName = "kg"
        };

        var tags = InspectionRecordPdfService.BuildPageTagValues(new[] { line });

        Assert.Equal("2025/03/01", tags["DELVDATE"]);
        Assert.Equal("1001", tags["ORDERNO00"]);
        Assert.Equal("P001", tags["ITEMCD00"]);
        Assert.Equal("親品目", tags["ITEMNM00"]);
        Assert.Equal("1/4", tags["STANDARD00"]);
        Assert.Equal("12.5", tags["QUANTITY00"]);
        Assert.Equal("kg", tags["QUANTITY17"]);
    }
}
