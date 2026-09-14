using BaggingInstructions.Api.Services;
using Xunit;

namespace BaggingInstructions.Api.Tests;

public class PurchaseOrderUnitConverterTests
{
    [Fact]
    public void PurchaseOrderUnitUnset_KeepsUnit0()
    {
        var (qty, unit) = PurchaseOrderUnitConverter.FromUnit0(
            120m, "  ", "0002", "0013", null, null, 12m, null, null, "", "kg");

        Assert.Equal(120m, qty);
        Assert.Equal("kg", unit);
    }

    [Fact]
    public void PurchaseOrderUnitIsUnit0_KeepsQtyAndUsesUnit0Name()
    {
        var (qty, unit) = PurchaseOrderUnitConverter.FromUnit0(
            120m, "0002", "0002", "0013", null, null, 12m, null, null, "kg", "kg");

        Assert.Equal(120m, qty);
        Assert.Equal("kg", unit);
    }

    [Fact]
    public void PurchaseOrderUnitIsUnit1_DividesByConversionValue1()
    {
        var (qty, unit) = PurchaseOrderUnitConverter.FromUnit0(
            120m, "0013", "0002", "0013", null, null, 12m, null, null, "ケース", "kg");

        Assert.Equal(10m, qty);
        Assert.Equal("ケース", unit);
    }

    [Fact]
    public void PurchaseOrderUnitIsUnit2_DividesByConversionValue2()
    {
        var (qty, unit) = PurchaseOrderUnitConverter.FromUnit0(
            120m, "0021", "0002", "0013", "0021", null, 12m, 24m, null, "箱", "kg");

        Assert.Equal(5m, qty);
        Assert.Equal("箱", unit);
    }

    [Fact]
    public void PurchaseOrderUnitIsUnit3_DividesByConversionValue3()
    {
        var (qty, unit) = PurchaseOrderUnitConverter.FromUnit0(
            120m, "0029", "0002", "0013", "0021", "0029", 12m, 24m, 60m, "パレット", "kg");

        Assert.Equal(2m, qty);
        Assert.Equal("パレット", unit);
    }

    [Fact]
    public void ConversionValueMissing_FallsBackToUnit0()
    {
        var (qty, unit) = PurchaseOrderUnitConverter.FromUnit0(
            120m, "0013", "0002", "0013", null, null, null, null, null, "ケース", "kg");

        Assert.Equal(120m, qty);
        Assert.Equal("kg", unit);
    }

    [Fact]
    public void PurchaseOrderUnitMatchesNoSlot_FallsBackToUnit0()
    {
        var (qty, unit) = PurchaseOrderUnitConverter.FromUnit0(
            120m, "9999", "0002", "0013", null, null, 12m, null, null, "謎単位", "kg");

        Assert.Equal(120m, qty);
        Assert.Equal("kg", unit);
    }

    [Fact]
    public void PurchaseOrderUnitNameMissing_FallsBackToUnit0Name()
    {
        var (qty, unit) = PurchaseOrderUnitConverter.FromUnit0(
            120m, "0013", "0002", " 0013 ", null, null, 12m, null, null, "", "kg");

        Assert.Equal(10m, qty);
        Assert.Equal("kg", unit);
    }
}
