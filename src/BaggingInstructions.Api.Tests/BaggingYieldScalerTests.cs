using BaggingInstructions.Api.DTOs;
using BaggingInstructions.Api.Services;

namespace BaggingInstructions.Api.Tests;

public class BaggingYieldScalerTests
{
    [Fact]
    public void ApplyParentYield_NullOrNonPositive_NoChange()
    {
        var items = new List<BaggingInstructionItemDto>
        {
            new() { PlannedQuantity = 10m },
            new() { PlannedQuantity = 20m }
        };
        BaggingYieldScaler.ApplyParentYieldToPlannedQuantities(items, null);
        Assert.Equal(10m, items[0].PlannedQuantity);
        Assert.Equal(20m, items[1].PlannedQuantity);

        BaggingYieldScaler.ApplyParentYieldToPlannedQuantities(items, 0m);
        Assert.Equal(10m, items[0].PlannedQuantity);

        BaggingYieldScaler.ApplyParentYieldToPlannedQuantities(items, -5m);
        Assert.Equal(10m, items[0].PlannedQuantity);
    }

    [Fact]
    public void ApplyParentYield_EmptyList_NoThrow()
    {
        BaggingYieldScaler.ApplyParentYieldToPlannedQuantities(new List<BaggingInstructionItemDto>(), 100m);
    }

    [Fact]
    public void ApplyParentYield_ZeroTotalOrder_NoChange()
    {
        var items = new List<BaggingInstructionItemDto>
        {
            new() { PlannedQuantity = 0m },
            new() { PlannedQuantity = 0m }
        };
        BaggingYieldScaler.ApplyParentYieldToPlannedQuantities(items, 50m);
        Assert.Equal(0m, items[0].PlannedQuantity);
    }

    [Fact]
    public void ApplyParentYield_ScalesByOrderShare()
    {
        var items = new List<BaggingInstructionItemDto>
        {
            new() { PlannedQuantity = 30m },
            new() { PlannedQuantity = 70m }
        };
        BaggingYieldScaler.ApplyParentYieldToPlannedQuantities(items, 50m);
        Assert.Equal(15m, items[0].PlannedQuantity);
        Assert.Equal(35m, items[1].PlannedQuantity);
    }
}
