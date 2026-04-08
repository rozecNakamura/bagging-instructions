using BaggingInstructions.Api.DTOs;
using BaggingInstructions.Api.Services;

namespace BaggingInstructions.Api.Tests;

public class BaggingBagCountResolverTests
{
    [Fact]
    public void UseFloorBagsFromCar0_NullItem_ReturnsFalse()
    {
        Assert.False(BaggingBagCountResolver.UseFloorBagsFromCar0(null));
    }

    [Fact]
    public void UseFloorBagsFromCar0_Liquid_ReturnsFalse()
    {
        var item = new ItemDetailDto { Itemcd = "551234", Uni = new UniDetailDto { Uninm = "ｇ" } };
        Assert.False(BaggingBagCountResolver.UseFloorBagsFromCar0(item));
    }

    [Fact]
    public void UseFloorBagsFromCar0_CountUnit_ReturnsFalse()
    {
        var item = new ItemDetailDto { Itemcd = "100", Uni = new UniDetailDto { Uninm = "個" } };
        Assert.False(BaggingBagCountResolver.UseFloorBagsFromCar0(item));
    }

    [Fact]
    public void UseFloorBagsFromCar0_NonLiquidNonCount_ReturnsTrue()
    {
        var item = new ItemDetailDto { Itemcd = "200", Uni = new UniDetailDto { Uninm = "ｇ" } };
        Assert.True(BaggingBagCountResolver.UseFloorBagsFromCar0(item));
    }
}
