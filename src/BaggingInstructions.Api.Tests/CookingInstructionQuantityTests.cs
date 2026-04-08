using BaggingInstructions.Api.Services;

namespace BaggingInstructions.Api.Tests;

public class CookingInstructionQuantityTests
{
    [Fact]
    public void ToParentQtyInUnit0_divides_by_std_when_set()
    {
        var q = CookingInstructionQuantity.ToParentQtyInUnit0(100m, "4", null, null, null);
        Assert.Equal(25m, q);
    }

    [Fact]
    public void ToParentQtyInUnit0_divides_by_car0_when_std_missing()
    {
        var q = CookingInstructionQuantity.ToParentQtyInUnit0(100m, null, null, null, 5m);
        Assert.Equal(20m, q);
    }

    [Fact]
    public void ToParentQtyInUnit0_uses_std2_when_std1_empty()
    {
        var q = CookingInstructionQuantity.ToParentQtyInUnit0(100m, "", "5", null, null);
        Assert.Equal(20m, q);
    }

    [Fact]
    public void ParentDisplayForPdf_uses_procurement_when_unit1_and_name_set()
    {
        var (qty, unit) = CookingInstructionQuantity.ParentDisplayForPdf(100m, "箱", "g", 10m);
        Assert.Equal(10m, qty);
        Assert.Equal("箱", unit);
    }

    [Fact]
    public void ParentDisplayForPdf_falls_back_to_unit0_when_no_procurement()
    {
        var (qty, unit) = CookingInstructionQuantity.ParentDisplayForPdf(100m, null, "g", null);
        Assert.Equal(100m, qty);
        Assert.Equal("g", unit);
    }

    [Fact]
    public void ParentDisplayForPdf_stays_unit0_when_procurement_name_but_conversion_value1_null()
    {
        var (qty, unit) = CookingInstructionQuantity.ParentDisplayForPdf(100m, "箱", "g", null);
        Assert.Equal(100m, qty);
        Assert.Equal("g", unit);
    }

    [Fact]
    public void ParentDisplayForPdf_stays_unit0_when_procurement_name_but_conversion_value1_zero()
    {
        var (qty, unit) = CookingInstructionQuantity.ParentDisplayForPdf(100m, "箱", "g", 0m);
        Assert.Equal(100m, qty);
        Assert.Equal("g", unit);
    }

    [Fact]
    public void ResolveParentQtyInUnit0_prefers_qtyuni0()
    {
        var q = CookingInstructionQuantity.ResolveParentQtyInUnit0(
            999m, 42m, 1m, null, null, null, null, null, null, 10m, 5m, 2m);
        Assert.Equal(42m, q);
    }

    [Fact]
    public void ResolveParentQtyInUnit0_derives_from_qtyuni1_times_conversionvalue1_when_no_uni0()
    {
        var q = CookingInstructionQuantity.ResolveParentQtyInUnit0(
            100m, null, 3m, null, null, null, null, null, null, 10m, null, null);
        Assert.Equal(30m, q);
    }

    [Fact]
    public void ResolveParentQtyInUnit0_uses_qtyuni2_when_uni0_and_uni1_unusable()
    {
        var q = CookingInstructionQuantity.ResolveParentQtyInUnit0(
            100m, null, null, 4m, null, null, null, null, null, 10m, 3m, null);
        Assert.Equal(12m, q);
    }

    [Fact]
    public void ParentPlannedQtyDisplay_uses_qtyuni1_when_procurement_name_present()
    {
        var (qty, unit) = CookingInstructionQuantity.ParentPlannedQtyDisplay(999m, 7m, "箱", "g", 10m);
        Assert.Equal(7m, qty);
        Assert.Equal("箱", unit);
    }
}
