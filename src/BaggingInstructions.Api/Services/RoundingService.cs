using BaggingInstructions.Api.DTOs;

namespace BaggingInstructions.Api.Services;

public static class RoundingService
{
    private static readonly HashSet<string> CountUnitNames = new(StringComparer.Ordinal) { "個", "ケ", "ヶ", "箇", "コ" };

    /// <summary>新DB用：除数と調味液BOMリストで端数切り上げ・調味液計算。戻り値: (整数部分, 端数, 調味液情報リスト)</summary>
    public static (decimal IntegerPart, decimal RoundedRemainder, List<SeasoningAmountDto> SeasoningList) RoundUpQuantityWithSeasoning(
        decimal jobordqun,
        decimal divisor,
        IReadOnlyList<SeasoningBomRow> seasoningBoms)
    {
        if (seasoningBoms == null || seasoningBoms.Count == 0)
            return (jobordqun, 0, new List<SeasoningAmountDto>());
        if (divisor <= 0)
            return (jobordqun, 0, new List<SeasoningAmountDto>());

        var integerPart = (decimal)(long)(jobordqun / divisor);
        var actualRemainder = jobordqun % divisor;
        const decimal epsilon = 0.0000000001m;

        if (actualRemainder > epsilon)
        {
            var roundedRemainder = Math.Ceiling(actualRemainder);
            var list = new List<SeasoningAmountDto>();
            foreach (var row in seasoningBoms)
            {
                decimal calculatedAmount;
                if (row.ChildUnitName != null && CountUnitNames.Contains(row.ChildUnitName))
                    calculatedAmount = roundedRemainder;
                else
                {
                    var otp = row.Otp;
                    var amu = row.Amu;
                    if (otp == 0)
                        calculatedAmount = 0;
                    else
                        calculatedAmount = (roundedRemainder / otp) * amu;
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
            return (integerPart, roundedRemainder, list);
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
}
