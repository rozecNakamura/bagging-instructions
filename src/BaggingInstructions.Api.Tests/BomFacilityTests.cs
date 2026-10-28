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

    [Theory]
    [InlineData("HAKATA", "TOKYO", "HAKATA")]
    [InlineData(" HAKATA ", null, "HAKATA")]
    [InlineData(null, "TOKYO", "TOKYO")]
    [InlineData("  ", " TOKYO ", "TOKYO")]
    [InlineData(null, null, "MATSUYAMA")]
    [InlineData("", " ", "MATSUYAMA")]
    public void Resolve_製造オーダー優先_次に受注明細_無ければMATSUYAMA(string? order, string? salesOrderLine, string expected)
    {
        Assert.Equal(expected, BomFacility.Resolve(order, salesOrderLine));
    }

    [Fact]
    public void SqlResolve_製造オーダー優先で受注明細と既定値に落とす式を組み立てる()
    {
        Assert.Equal(
            "COALESCE(NULLIF(TRIM(BOTH FROM ot.facilitycode), ''), NULLIF(TRIM(BOTH FROM sol.facilitycode), ''), 'MATSUYAMA')",
            BomFacility.SqlResolve("ot.facilitycode", "sol.facilitycode"));
    }
}
