using BaggingInstructions.Api.DTOs;
using BaggingInstructions.Api.Services;

namespace BaggingInstructions.Api.Tests;

public class BaggingInstructionItemAggregatorTests
{
    [Fact]
    public void Merge_same_location_slot_and_addinfo05_different_customer_codes_merge_quantities()
    {
        var a = new BaggingInstructionItemDto
        {
            Shpctrcd = "LOC1",
            Itemcd = "401001",
            Delvedt = "20260101",
            Shptm = "08",
            Addinfo05 = "1",
            EatingTimeLabel = "朝",
            PlannedQuantity = 5m,
            AdjustedQuantity = 5m,
            QuantityForInventory = 5m,
            QuantityForInstruction = 5m,
            StandardBags = 0,
            IrregularQuantity = 0m,
            Shpctr = new ShpctrDetailDto { Cuscd = "CUST_A" }
        };
        var b = new BaggingInstructionItemDto
        {
            Shpctrcd = "LOC1",
            Itemcd = "401001",
            Delvedt = "20260101",
            Shptm = "08",
            Addinfo05 = "1",
            EatingTimeLabel = "朝",
            PlannedQuantity = 3m,
            AdjustedQuantity = 3m,
            QuantityForInventory = 3m,
            QuantityForInstruction = 3m,
            StandardBags = 0,
            IrregularQuantity = 0m,
            Shpctr = new ShpctrDetailDto { Cuscd = "CUST_B" }
        };

        var merged = BaggingInstructionItemAggregator.Merge(new[] { a, b });

        var row = Assert.Single(merged);
        Assert.Equal(8m, row.PlannedQuantity);
        Assert.Equal(8m, row.QuantityForInventory);
        Assert.Equal("朝", row.EatingTimeLabel);
        Assert.Equal("1", row.Addinfo05);
    }

    [Fact]
    public void Merge_different_addinfo05_stays_separate()
    {
        var morning = new BaggingInstructionItemDto
        {
            Shpctrcd = "LOC1",
            Itemcd = "401001",
            Delvedt = "20260101",
            Shptm = "08",
            Addinfo05 = "1",
            EatingTimeLabel = "朝",
            PlannedQuantity = 2m,
            AdjustedQuantity = 2m,
            QuantityForInventory = 2m,
            QuantityForInstruction = 2m,
            StandardBags = 0,
            IrregularQuantity = 0m
        };
        var noon = new BaggingInstructionItemDto
        {
            Shpctrcd = "LOC1",
            Itemcd = "401001",
            Delvedt = "20260101",
            Shptm = "08",
            Addinfo05 = "2",
            EatingTimeLabel = "昼",
            PlannedQuantity = 4m,
            AdjustedQuantity = 4m,
            QuantityForInventory = 4m,
            QuantityForInstruction = 4m,
            StandardBags = 0,
            IrregularQuantity = 0m
        };

        var merged = BaggingInstructionItemAggregator.Merge(new[] { morning, noon });

        Assert.Equal(2, merged.Count);
    }

    [Fact]
    public void Merge_same_location_name_different_location_codes_merge_quantities()
    {
        var a = new BaggingInstructionItemDto
        {
            Shpctrcd = "LOC-A",
            Shpctrnm = "第一小学校",
            Itemcd = "401001",
            Delvedt = "20260101",
            Shptm = "08",
            Addinfo05 = "1",
            EatingTimeLabel = "朝",
            PlannedQuantity = 4m,
            AdjustedQuantity = 4m,
            QuantityForInventory = 4m,
            QuantityForInstruction = 4m,
            StandardBags = 1,
            IrregularQuantity = 0m
        };
        var b = new BaggingInstructionItemDto
        {
            Shpctrcd = "LOC-B",
            Shpctrnm = "第一小学校",
            Itemcd = "401001",
            Delvedt = "20260101",
            Shptm = "08",
            Addinfo05 = "1",
            EatingTimeLabel = "朝",
            PlannedQuantity = 6m,
            AdjustedQuantity = 6m,
            QuantityForInventory = 6m,
            QuantityForInstruction = 6m,
            StandardBags = 1,
            IrregularQuantity = 0m
        };

        var merged = BaggingInstructionItemAggregator.Merge(new[] { a, b });

        var row = Assert.Single(merged);
        Assert.Equal(10m, row.PlannedQuantity);
        Assert.Equal("第一小学校", row.Shpctrnm);
        Assert.Equal("LOC-A", row.Shpctrcd);
    }
}
