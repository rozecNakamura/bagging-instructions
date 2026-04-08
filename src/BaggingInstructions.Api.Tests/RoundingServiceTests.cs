using BaggingInstructions.Api.DTOs;
using BaggingInstructions.Api.Services;

namespace BaggingInstructions.Api.Tests;

public class RoundingServiceTests
{
    private static readonly SeasoningBomRow BomG = new()
    {
        ChildItemCd = "S1",
        ChildUnitName = "g",
        Otp = 1,
        Amu = 1
    };

    [Fact]
    public void RoundUp_CountUnit_CeilsRemainder()
    {
        var parent = new ItemDetailDto { Itemcd = "100", Uni = new UniDetailDto { Uninm = "個" } };
        var (std, irr, list) = RoundingService.RoundUpQuantityWithSeasoning(25m, 10m, new[] { BomG }, parent);
        Assert.Equal(2m, std);
        Assert.Equal(5m, irr);
        Assert.Single(list);
        Assert.Equal(5m, list[0].CalculatedAmount);
    }

    [Fact]
    public void RoundUp_NonCountUnit_KeepsActualRemainder()
    {
        var parent = new ItemDetailDto { Itemcd = "200", Uni = new UniDetailDto { Uninm = "ｇ" } };
        var (std, irr, list) = RoundingService.RoundUpQuantityWithSeasoning(25.3m, 10m, new[] { BomG }, parent);
        Assert.Equal(2m, std);
        Assert.Equal(5.3m, irr);
        Assert.Equal(5.3m, list[0].CalculatedAmount);
    }

    [Fact]
    public void RoundUp_Liquid_NoStandardBags_LinearSeasoning()
    {
        var parent = new ItemDetailDto { Itemcd = "551234", Uni = new UniDetailDto { Uninm = "個" } };
        var boms = new[]
        {
            new SeasoningBomRow { ChildItemCd = "L1", Otp = 2, Amu = 3, ChildUnitName = "ml" }
        };
        var (std, irr, list) = RoundingService.RoundUpQuantityWithSeasoning(10m, 10m, boms, parent);
        Assert.Equal(0m, std);
        Assert.Equal(10m, irr);
        Assert.Single(list);
        Assert.Equal(15m, list[0].CalculatedAmount);
    }

    [Fact]
    public void FinishedGoodUsesCountRounding_UnknownUnit_DefaultsTrue()
    {
        Assert.True(RoundingService.FinishedGoodUsesCountRounding(new ItemDetailDto { Itemcd = "X" }));
    }
}
