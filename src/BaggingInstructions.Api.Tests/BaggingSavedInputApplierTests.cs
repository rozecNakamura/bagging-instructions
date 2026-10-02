using BaggingInstructions.Api.DTOs;
using BaggingInstructions.Api.Services;

namespace BaggingInstructions.Api.Tests;

public class BaggingSavedInputApplierTests
{
    [Fact]
    public void SplitIntProportional_PreservesTotal()
    {
        var weights = new List<decimal> { 30, 70 };
        var split = BaggingSavedInputApplier.SplitIntProportional(10, weights, 100);
        Assert.Equal(2, split.Count);
        Assert.Equal(10, split.Sum());
    }

    [Fact]
    public void ComputeDefaultGlobalTotals_UsesOtpAmu()
    {
        var mboms = new List<MbomDetailDto>
        {
            new() { Citemcd = "A", Otp = 2, Amu = 5 }
        };
        var g = BaggingSavedInputApplier.ComputeDefaultGlobalTotals(100, mboms);
        Assert.Single(g);
        Assert.Equal(250m, g[0]);
    }

    [Fact]
    public void ResolveGlobalTotals_UsesPayloadTotalQty()
    {
        var mboms = new List<MbomDetailDto>
        {
            new() { Citemcd = "X", Otp = 1, Amu = 1 }
        };
        var payload = new BaggingInputPayloadDto
        {
            Lines = new List<BaggingInputLineDto>
            {
                new() { Citemcd = "X", TotalQty = 99 }
            }
        };
        var g = BaggingSavedInputApplier.ResolveGlobalTotals(100, mboms, payload);
        Assert.Equal(99m, g[0]);
    }

    [Fact]
    public void ApplySavedInputPerFacilityRounding_RecalculatesBagsAndSeasoningPerRow()
    {
        var seasoningBoms = new List<SeasoningBomRow>
        {
            new() { Otp = 1, Amu = 1, ChildItemCd = "C1", ChildUnitName = "g" }
        };
        var mboms = new List<MbomDetailDto>
        {
            new()
            {
                Citemcd = "C1",
                Otp = 1,
                Amu = 1,
                ChildItem = new ItemDetailDto { Itemcd = "C1" }
            }
        };
        var items = new List<BaggingInstructionItemDto>
        {
            new() { PlannedQuantity = 25 },
            new() { PlannedQuantity = 75 }
        };

        var globals = new List<decimal> { 100m };
        BaggingSavedInputApplier.ApplySavedInputPerFacilityRounding(items, 10m, seasoningBoms, mboms, globals);

        Assert.Equal(2, items[0].StandardBags);
        Assert.Equal(5m, items[0].IrregularQuantity);
        Assert.Equal(7m, items[0].AdjustedQuantity);
        Assert.Single(items[0].SeasoningAmounts);
        Assert.Equal(25m, items[0].SeasoningAmounts[0].CalculatedAmount);
        Assert.Equal("C1", items[0].SeasoningAmounts[0].ChildItem?.Itemcd);

        Assert.Equal(7, items[1].StandardBags);
        Assert.Equal(5m, items[1].IrregularQuantity);
        Assert.Equal(12m, items[1].AdjustedQuantity);
        Assert.Equal(75m, items[1].SeasoningAmounts[0].CalculatedAmount);

        Assert.Equal(100m, items[0].SeasoningAmounts[0].CalculatedAmount + items[1].SeasoningAmounts[0].CalculatedAmount);
    }

    [Fact]
    public void ApplySavedInputPerFacilityRounding_WithoutGlobalTotals_UsesRemainderSeasoning()
    {
        var seasoningBoms = new List<SeasoningBomRow>
        {
            new() { Otp = 1, Amu = 1, ChildItemCd = "C1", ChildUnitName = "g" }
        };
        var mboms = new List<MbomDetailDto>
        {
            new() { Citemcd = "C1", ChildItem = new ItemDetailDto { Itemcd = "C1" } }
        };
        var items = new List<BaggingInstructionItemDto> { new() { PlannedQuantity = 25 } };

        BaggingSavedInputApplier.ApplySavedInputPerFacilityRounding(items, 10m, seasoningBoms, mboms, null);

        Assert.Single(items[0].SeasoningAmounts);
        Assert.Equal(5m, items[0].SeasoningAmounts[0].CalculatedAmount);
    }

    [Fact]
    public void ApplySavedInputPerFacilityRounding_uses_planned_quantities_and_global_split()
    {
        var seasoningBoms = new List<SeasoningBomRow>
        {
            new() { Otp = 1, Amu = 1, ChildItemCd = "C1", ChildUnitName = "g" }
        };
        var mboms = new List<MbomDetailDto>
        {
            new()
            {
                Citemcd = "C1",
                Otp = 1,
                Amu = 1,
                ChildItem = new ItemDetailDto { Itemcd = "C1" }
            }
        };
        var items = new List<BaggingInstructionItemDto>
        {
            new() { PlannedQuantity = 15m },
            new() { PlannedQuantity = 35m }
        };

        var globals = new List<decimal> { 100m };
        BaggingSavedInputApplier.ApplySavedInputPerFacilityRounding(items, 10m, seasoningBoms, mboms, globals);

        Assert.Equal(1, items[0].StandardBags);
        Assert.Equal(5m, items[0].IrregularQuantity);
        Assert.Equal(3, items[1].StandardBags);
        Assert.Equal(5m, items[1].IrregularQuantity);
        Assert.Equal(30m, items[0].SeasoningAmounts[0].CalculatedAmount);
        Assert.Equal(70m, items[1].SeasoningAmounts[0].CalculatedAmount);
    }

    [Fact]
    public void ApplySavedInputPerFacilityRounding_non_gram_first_child_uses_order_quantity_divided_by_spec()
    {
        var seasoningBoms = new List<SeasoningBomRow>
        {
            new() { Otp = 1, Amu = 1, ChildItemCd = "C1", ChildUnitName = "ml" }
        };
        var mboms = new List<MbomDetailDto>
        {
            new()
            {
                Citemcd = "C1",
                Proutno = 0,
                ChildItem = new ItemDetailDto
                {
                    Itemcd = "C1",
                    Uni = new UniDetailDto { Uninm = "ml" }
                }
            }
        };
        var items = new List<BaggingInstructionItemDto>
        {
            new()
            {
                PlannedQuantity = 13.2m,
                Item = new ItemDetailDto { Itemcd = "401001", Uni = new UniDetailDto { Uninm = "ｇ" } }
            }
        };

        BaggingSavedInputApplier.ApplySavedInputPerFacilityRounding(
            items, 6m, seasoningBoms, mboms, null, 6m);

        Assert.Equal(2, items[0].StandardBags);
        Assert.Equal(2m, items[0].IrregularQuantity);
    }

    [Fact]
    public void ApplySavedInputPerFacilityRounding_gram_first_child_uses_allocated_entered_quantity_divided_by_spec()
    {
        var seasoningBoms = new List<SeasoningBomRow>
        {
            new() { Otp = 1, Amu = 1, ChildItemCd = "C1", ChildUnitName = "g" }
        };
        var mboms = new List<MbomDetailDto>
        {
            new()
            {
                Citemcd = "C1",
                Proutno = 0,
                ChildItem = new ItemDetailDto
                {
                    Itemcd = "C1",
                    Uni = new UniDetailDto { Uninm = "g" }
                }
            }
        };
        var items = new List<BaggingInstructionItemDto>
        {
            new() { PlannedQuantity = 25m },
            new() { PlannedQuantity = 75m }
        };

        BaggingSavedInputApplier.ApplySavedInputPerFacilityRounding(
            items, 10m, seasoningBoms, mboms, new List<decimal> { 120m }, 10m, useIngredientAllocation: true);

        Assert.Equal(3, items[0].StandardBags);
        Assert.Equal(0m, items[0].IrregularQuantity);
        Assert.Equal(9, items[1].StandardBags);
        Assert.Equal(0m, items[1].IrregularQuantity);
    }

    [Fact]
    public void AllocateByOrderShare_PreservesTotal_WhenShareDoesNotDivideEvenly()
    {
        // 700/3100 は割り切れず、decimal 除算の丸めで 3100 × share が 700 に戻らない。
        var allocated = BaggingSavedInputApplier.AllocateByOrderShare(3100m, new List<decimal> { 700m, 2400m });

        Assert.Equal(700m, allocated[0]);
        Assert.Equal(2400m, allocated[1]);
        Assert.Equal(3100m, allocated.Sum());
    }

    [Fact]
    public void AllocateByOrderShare_PutsResidualOnLastElement()
    {
        // 100 ÷ 3 は割り切れないので、残差が消えずに最終要素へ寄ること。
        var allocated = BaggingSavedInputApplier.AllocateByOrderShare(100m, new List<decimal> { 1m, 1m, 1m });

        Assert.Equal(100m, allocated.Sum());
        Assert.Equal(33.333333m, allocated[0]);
        Assert.Equal(33.333333m, allocated[1]);
        Assert.Equal(33.333334m, allocated[2]);
    }

    [Fact]
    public void AllocateByOrderShare_ReturnsZeros_WhenNoOrderQuantity()
    {
        var allocated = BaggingSavedInputApplier.AllocateByOrderShare(100m, new List<decimal> { 0m, 0m });

        Assert.Equal(new List<decimal> { 0m, 0m }, allocated);
    }

    /// <summary>
    /// 2026-09-24 / 4090408014 の再発防止。受注が規格数量の倍数ちょうどの施設で
    /// 「1袋 + 端数700」という矛盾した値を返していた（画面側の補正と二重になり2袋に見えた）。
    /// </summary>
    [Fact]
    public void ApplySpecBagCounts_exact_multiple_of_spec_has_no_remainder()
    {
        var mboms = GramChildMboms();
        var items = new List<BaggingInstructionItemDto>
        {
            new() { PlannedQuantity = 700m },
            new() { PlannedQuantity = 2400m }
        };

        BaggingSavedInputApplier.ApplySpecBagCounts(
            items, 700m, mboms, new List<decimal> { 3100m }, useIngredientAllocation: true);

        Assert.Equal(1, items[0].StandardBags);
        Assert.Equal(0m, items[0].IrregularQuantity);
        Assert.Equal(3, items[1].StandardBags);
        Assert.Equal(300m, items[1].IrregularQuantity);
    }

    [Theory]
    // 規格ちょうどで割り切れる施設を含むケース（今回の不具合）
    [InlineData(700, 3100, new[] { 700, 2400 })]
    // 施設数が多く按分の残差が散りやすいケース
    [InlineData(1200, 83990, new[] { 6361, 31287, 17003, 7809, 5187, 7125, 2052, 7603 })]
    // 総数量が受注合計と一致しない（投入量を手入力した）ケース
    [InlineData(700, 5974, new[] { 171, 1983, 741, 2622, 399, 57 })]
    public void ApplySpecBagCounts_total_matches_entered_quantity(int spec, int total, int[] orders)
    {
        var mboms = GramChildMboms();
        var items = orders.Select(q => new BaggingInstructionItemDto { PlannedQuantity = q }).ToList();

        BaggingSavedInputApplier.ApplySpecBagCounts(
            items, spec, mboms, new List<decimal> { total }, useIngredientAllocation: true);

        // 帳票の内部整合: Σ(規格袋数 × 規格数量 + 端数) == 総数量
        var sum = items.Sum(i => i.StandardBags * (decimal)spec + i.IrregularQuantity);
        Assert.Equal((decimal)total, sum);

        // 端数が規格数量以上になると画面側が「袋が足りない」と誤認する
        Assert.All(items, i => Assert.True(i.IrregularQuantity < spec));
        Assert.All(items, i => Assert.True(i.IrregularQuantity >= 0m));
    }

    [Fact]
    public void ApplySavedInputPerFacilityRounding_seasoning_allocation_sums_to_global_total()
    {
        var seasoningBoms = new List<SeasoningBomRow>
        {
            new() { Otp = 1, Amu = 1, ChildItemCd = "C1", ChildUnitName = "g" }
        };
        var mboms = GramChildMboms();
        var items = new List<BaggingInstructionItemDto>
        {
            new() { PlannedQuantity = 700m },
            new() { PlannedQuantity = 2400m }
        };

        BaggingSavedInputApplier.ApplySavedInputPerFacilityRounding(
            items, 700m, seasoningBoms, mboms, new List<decimal> { 3100m }, 700m, useIngredientAllocation: true);

        Assert.Equal(700m, items[0].SeasoningAmounts[0].CalculatedAmount);
        Assert.Equal(2400m, items[1].SeasoningAmounts[0].CalculatedAmount);
        Assert.Equal(3100m, items.Sum(i => i.SeasoningAmounts[0].CalculatedAmount));
    }

    private static List<MbomDetailDto> GramChildMboms() => new()
    {
        new MbomDetailDto
        {
            Citemcd = "C1",
            Proutno = 0,
            Otp = 1,
            Amu = 1,
            ChildItem = new ItemDetailDto
            {
                Itemcd = "C1",
                Uni = new UniDetailDto { Uninm = "g" }
            }
        }
    };
}
