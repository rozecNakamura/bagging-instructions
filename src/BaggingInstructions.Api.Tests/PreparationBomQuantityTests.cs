using BaggingInstructions.Api.Services;

namespace BaggingInstructions.Api.Tests;

public class PreparationBomQuantityTests
{
    [Fact]
    public void ComputeRequiredQty_standardBom_yield100()
    {
        // 歩留まり100%は補正なし: 100 * (2/1) / 1.0 = 200
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
    public void ComputeRequiredQty_yieldHalf_dividesYield()
    {
        // 歩留まり50%は除算: 100 * (1/1) / 0.5 = 200（損失分だけ多く必要）
        var q = PreparationBomQuantity.ComputeRequiredQty(100m, 1m, 1m, 50m);
        Assert.Equal(200m, q);
    }

    [Fact]
    public void ComputeRequiredQty_yieldZero_treatedAs100Percent()
    {
        // yieldpercent=0 は補正なし扱い: 100 * (1/1) / 1.0 = 100
        var q = PreparationBomQuantity.ComputeRequiredQty(100m, 1m, 1m, 0m);
        Assert.Equal(100m, q);
    }

    [Fact]
    public void ComputeRequiredQty_userExample()
    {
        // 798g製造、1gあたり0.3052g必要、歩留まり75%: 798 * 0.3052 / 0.75
        var expected = 798m * 0.3052m / 0.75m;
        var q = PreparationBomQuantity.ComputeRequiredQty(798m, 0.3052m, 1m, 75m);
        Assert.Equal(expected, q);
    }
}
