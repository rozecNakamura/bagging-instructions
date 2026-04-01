using BaggingInstructions.Api.Services;

namespace BaggingInstructions.Api.Tests;

public class PreparationBomQuantityTests
{
    [Fact]
    public void ComputeRequiredQty_standardBom_multipliesYield()
    {
        var q = PreparationBomQuantity.ComputeRequiredQty(100m, 2m, 1m, 100m);
        Assert.Equal(200m, q);
    }

    [Fact]
    public void ComputeRequiredQty_outputZero_returnsZero()
    {
        var q = PreparationBomQuantity.ComputeRequiredQty(100m, 2m, 0m, 100m);
        Assert.Equal(0m, q);
    }

    [Fact]
    public void ComputeRequiredQty_yieldHalf()
    {
        var q = PreparationBomQuantity.ComputeRequiredQty(100m, 1m, 1m, 50m);
        Assert.Equal(50m, q);
    }
}
