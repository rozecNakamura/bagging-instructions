using BaggingInstructions.Api.DTOs;
using BaggingInstructions.Api.Services;

namespace BaggingInstructions.Api.Tests;

public class BaggingOrderBasedRoundingTests
{
    private static readonly SeasoningBomRow BomG = new()
    {
        ChildItemCd = "S1",
        ChildUnitName = "g",
        Otp = 1,
        Amu = 1
    };

    [Fact]
    public void CountUnit_parent_does_not_apply_floor_allocation_after_rounding()
    {
        var parent = new ItemDetailDto { Itemcd = "100", Uni = new UniDetailDto { Uninm = "個" } };
        var seasoning = new List<SeasoningBomRow> { BomG };

        var (adj, stdBags, irr, _) = BaggingOrderBasedRounding.ApplyRoundingAndOptionalFloorAllocation(
            25m, 10m, 10m, parent, seasoning);

        Assert.Equal(7m, adj);
        Assert.Equal(2, stdBags);
        Assert.Equal(5m, irr);
    }

    [Fact]
    public void NonCount_nonLiquid_applies_floor_bags_from_car0_after_rounding()
    {
        var parent = new ItemDetailDto { Itemcd = "200", Uni = new UniDetailDto { Uninm = "ｇ" } };
        var seasoning = new List<SeasoningBomRow> { BomG };

        var (adj, stdBags, irr, _) = BaggingOrderBasedRounding.ApplyRoundingAndOptionalFloorAllocation(
            25.3m, 10m, 10m, parent, seasoning);

        Assert.Equal(7.3m, adj);
        Assert.Equal(0, stdBags);
        Assert.Equal(7.3m, irr);
    }

    [Fact]
    public void Liquid_parent_never_uses_floor_allocation()
    {
        var parent = new ItemDetailDto { Itemcd = "551234", Uni = new UniDetailDto { Uninm = "ｇ" } };
        var seasoning = new List<SeasoningBomRow>
        {
            new() { ChildItemCd = "L1", Otp = 2, Amu = 3, ChildUnitName = "ml" }
        };

        var (adj, stdBags, irr, list) = BaggingOrderBasedRounding.ApplyRoundingAndOptionalFloorAllocation(
            10m, 10m, 5m, parent, seasoning);

        Assert.Equal(10m, adj);
        Assert.Equal(0, stdBags);
        Assert.Equal(10m, irr);
        Assert.Single(list);
    }
}
