using BaggingInstructions.Api.Services;

namespace BaggingInstructions.Api.Tests;

public class BomFacilityTests
{
    [Fact]
    public void Resolve_受注明細に工場コードがあればそれを使う()
    {
        Assert.Equal("TOUON", BomFacility.Resolve("TOUON"));
    }

    [Fact]
    public void Resolve_前後の空白は除去する()
    {
        Assert.Equal("TOUON", BomFacility.Resolve("  TOUON  "));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_未設定なら既定のMATSUYAMA(string? facilityCode)
    {
        Assert.Equal("MATSUYAMA", BomFacility.Resolve(facilityCode));
        Assert.Equal(BomFacility.Default, BomFacility.Resolve(facilityCode));
    }

    [Fact]
    public void SqlResolve_受注明細の列から既定値付きの式を組み立てる()
    {
        Assert.Equal(
            "COALESCE(NULLIF(TRIM(BOTH FROM sol.facilitycode), ''), 'MATSUYAMA')",
            BomFacility.SqlResolve("sol.facilitycode"));
    }
}
