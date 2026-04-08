using BaggingInstructions.Api.DTOs;
using BaggingInstructions.Api.Services;

namespace BaggingInstructions.Api.Tests;

public class BaggingSavedInputApplierTests
{
    [Fact]
    public void SplitIntProportional_PreservesTotal()
    {
        var weights = new List<decimal> { 30, 70 };
        var split = BaggingSavedInputApplier.SplitIntProportional(10, weights, 100);
        Assert.Equal(2, split.Count);
        Assert.Equal(10, split.Sum());
    }

    [Fact]
    public void ComputeDefaultGlobalTotals_UsesOtpAmu()
    {
        var mboms = new List<MbomDetailDto>
        {
            new() { Citemcd = "A", Otp = 2, Amu = 5 }
        };
        var g = BaggingSavedInputApplier.ComputeDefaultGlobalTotals(100, mboms);
        Assert.Single(g);
        Assert.Equal(250m, g[0]);
    }

    [Fact]
    public void ResolveGlobalTotals_UsesPayloadTotalQty()
    {
        var mboms = new List<MbomDetailDto>
        {
            new() { Citemcd = "X", Otp = 1, Amu = 1 }
        };
        var payload = new BaggingInputPayloadDto
        {
            Lines = new List<BaggingInputLineDto>
            {
                new() { Citemcd = "X", TotalQty = 99 }
            }
        };
        var g = BaggingSavedInputApplier.ResolveGlobalTotals(100, mboms, payload);
        Assert.Equal(99m, g[0]);
    }

    [Fact]
    public void ApplySavedInputPerFacilityRounding_RecalculatesBagsAndSeasoningPerRow()
    {
        var seasoningBoms = new List<SeasoningBomRow>
        {
            new() { Otp = 1, Amu = 1, ChildItemCd = "C1", ChildUnitName = "g" }
        };
        var mboms = new List<MbomDetailDto>
        {
            new()
            {
                Citemcd = "C1",
                Otp = 1,
                Amu = 1,
                ChildItem = new ItemDetailDto { Itemcd = "C1" }
            }
        };
        var items = new List<BaggingInstructionItemDto>
        {
            new() { PlannedQuantity = 25 },
            new() { PlannedQuantity = 75 }
        };

        var globals = new List<decimal> { 100m };
        BaggingSavedInputApplier.ApplySavedInputPerFacilityRounding(items, 10m, seasoningBoms, mboms, globals);

        Assert.Equal(2, items[0].StandardBags);
        Assert.Equal(5m, items[0].IrregularQuantity);
        Assert.Equal(7m, items[0].AdjustedQuantity);
        Assert.Single(items[0].SeasoningAmounts);
        Assert.Equal(25m, items[0].SeasoningAmounts[0].CalculatedAmount);
        Assert.Equal("C1", items[0].SeasoningAmounts[0].ChildItem?.Itemcd);

        Assert.Equal(7, items[1].StandardBags);
        Assert.Equal(5m, items[1].IrregularQuantity);
        Assert.Equal(12m, items[1].AdjustedQuantity);
        Assert.Equal(75m, items[1].SeasoningAmounts[0].CalculatedAmount);

        Assert.Equal(100m, items[0].SeasoningAmounts[0].CalculatedAmount + items[1].SeasoningAmounts[0].CalculatedAmount);
    }

    [Fact]
    public void ApplySavedInputPerFacilityRounding_WithoutGlobalTotals_UsesRemainderSeasoning()
    {
        var seasoningBoms = new List<SeasoningBomRow>
        {
            new() { Otp = 1, Amu = 1, ChildItemCd = "C1", ChildUnitName = "g" }
        };
        var mboms = new List<MbomDetailDto>
        {
            new() { Citemcd = "C1", ChildItem = new ItemDetailDto { Itemcd = "C1" } }
        };
        var items = new List<BaggingInstructionItemDto> { new() { PlannedQuantity = 25 } };

        BaggingSavedInputApplier.ApplySavedInputPerFacilityRounding(items, 10m, seasoningBoms, mboms, null);

        Assert.Single(items[0].SeasoningAmounts);
        Assert.Equal(5m, items[0].SeasoningAmounts[0].CalculatedAmount);
    }

    [Fact]
    public void ApplySavedInputPerFacilityRounding_uses_planned_quantities_and_global_split()
    {
        var seasoningBoms = new List<SeasoningBomRow>
        {
            new() { Otp = 1, Amu = 1, ChildItemCd = "C1", ChildUnitName = "g" }
        };
        var mboms = new List<MbomDetailDto>
        {
            new()
            {
                Citemcd = "C1",
                Otp = 1,
                Amu = 1,
                ChildItem = new ItemDetailDto { Itemcd = "C1" }
            }
        };
        var items = new List<BaggingInstructionItemDto>
        {
            new() { PlannedQuantity = 15m },
            new() { PlannedQuantity = 35m }
        };

        var globals = new List<decimal> { 100m };
        BaggingSavedInputApplier.ApplySavedInputPerFacilityRounding(items, 10m, seasoningBoms, mboms, globals);

        Assert.Equal(1, items[0].StandardBags);
        Assert.Equal(5m, items[0].IrregularQuantity);
        Assert.Equal(3, items[1].StandardBags);
        Assert.Equal(5m, items[1].IrregularQuantity);
        Assert.Equal(30m, items[0].SeasoningAmounts[0].CalculatedAmount);
        Assert.Equal(70m, items[1].SeasoningAmounts[0].CalculatedAmount);
    }
}
