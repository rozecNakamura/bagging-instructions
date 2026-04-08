using BaggingInstructions.Api.DTOs;

namespace BaggingInstructions.Api.Services;

public static class RoundingService
{
    private static readonly HashSet<string> CountUnitNames = new(StringComparer.Ordinal) { "個", "ケ", "ヶ", "箇", "コ" };

    /// <summary>単位0が個数系なら端数を切り上げ。未設定は従来互換で個数扱い。</summary>
    public static bool FinishedGoodUsesCountRounding(ItemDetailDto? parentItem)
    {
        var u = parentItem?.Uni?.Uninm;
        if (string.IsNullOrWhiteSpace(u)) return true;
        foreach (var t in CountUnitNames)
        {
            if (u.Contains(t, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    /// <summary>新DB用：除数と調味液BOM。液体(55始まり)は袋分割せず線形配合。個数以外の完成品は端数切上げなし。</summary>
    public static (decimal IntegerPart, decimal RoundedRemainder, List<SeasoningAmountDto> SeasoningList) RoundUpQuantityWithSeasoning(
        decimal jobordqun,
        decimal divisor,
        IReadOnlyList<SeasoningBomRow> seasoningBoms,
        ItemDetailDto? parentItem = null)
    {
        if (seasoningBoms == null || seasoningBoms.Count == 0)
            return (jobordqun, 0, new List<SeasoningAmountDto>());
        if (divisor <= 0)
            return (jobordqun, 0, new List<SeasoningAmountDto>());

        if (ItemCodeKind.IsLiquid(parentItem?.Itemcd))
            return RoundLiquid(jobordqun, seasoningBoms);

        var integerPart = (decimal)(long)(jobordqun / divisor);
        var actualRemainder = jobordqun % divisor;
        const decimal epsilon = 0.0000000001m;
        var countRounding = FinishedGoodUsesCountRounding(parentItem);

        if (actualRemainder > epsilon)
        {
            var remainderForSeasoning = countRounding ? Math.Ceiling(actualRemainder) : actualRemainder;
            var list = new List<SeasoningAmountDto>();
            foreach (var row in seasoningBoms)
            {
                decimal calculatedAmount;
                if (row.ChildUnitName != null && CountUnitNames.Contains(row.ChildUnitName))
                    calculatedAmount = remainderForSeasoning;
                else
                {
                    var otp = row.Otp;
                    var amu = row.Amu;
                    if (otp == 0)
                        calculatedAmount = 0;
                    else
                        calculatedAmount = (remainderForSeasoning / otp) * amu;
                }

                list.Add(new SeasoningAmountDto
                {
                    Citemcd = row.ChildItemCd ?? "",
                    Citemgr = null,
                    Cfctcd = null,
                    Cdeptcd = null,
                    Amu = row.Amu,
                    Otp = row.Otp,
                    CalculatedAmount = calculatedAmount,
                    ChildItem = null
                });
            }

            return (integerPart, remainderForSeasoning, list);
        }

        var zeroList = seasoningBoms.Select(row => new SeasoningAmountDto
        {
            Citemcd = row.ChildItemCd ?? "",
            Amu = row.Amu,
            Otp = row.Otp,
            CalculatedAmount = 0,
            ChildItem = null
        }).ToList();
        return (integerPart, 0, zeroList);
    }

    /// <summary>液体：規格袋数は出さず、受注数量を端数欄相当で一括、子品目は受注に対する配合比。</summary>
    private static (decimal IntegerPart, decimal IrregularPart, List<SeasoningAmountDto> SeasoningList) RoundLiquid(
        decimal jobordqun,
        IReadOnlyList<SeasoningBomRow> seasoningBoms)
    {
        var list = new List<SeasoningAmountDto>();
        foreach (var row in seasoningBoms)
        {
            var otp = row.Otp;
            var amu = row.Amu;
            var calculatedAmount = otp == 0 ? 0 : (jobordqun / otp) * amu;
            list.Add(new SeasoningAmountDto
            {
                Citemcd = row.ChildItemCd ?? "",
                Amu = row.Amu,
                Otp = row.Otp,
                CalculatedAmount = calculatedAmount,
                ChildItem = null
            });
        }

        return (0, jobordqun, list);
    }
}
