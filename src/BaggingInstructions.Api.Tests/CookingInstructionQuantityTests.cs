using BaggingInstructions.Api.Services;

namespace BaggingInstructions.Api.Tests;

public class CookingInstructionQuantityTests
{
    [Fact]
    public void ToParentQtyInUnit0_divides_by_std_when_set()
    {
        var q = CookingInstructionQuantity.ToParentQtyInUnit0(100m, "4", null);
        Assert.Equal(25m, q);
    }

    [Fact]
    public void ToParentQtyInUnit0_divides_by_car0_when_std_missing()
    {
        var q = CookingInstructionQuantity.ToParentQtyInUnit0(100m, null, 5m);
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
}
