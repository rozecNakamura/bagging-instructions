using BaggingInstructions.Api.Entities;
using BaggingInstructions.Api.DTOs;

namespace BaggingInstructions.Api.Services;

public static class LabelGeneratorService
{
    private const int DefaultDaysAfter = 3;

    public static string CalculateExpiryDate(string delvedt, int daysAfter = DefaultDaysAfter)
    {
        if (string.IsNullOrEmpty(delvedt) || delvedt.Length != 8) return "";
        if (!DateTime.TryParseExact(delvedt, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var dateObj))
            return "";
        return dateObj.AddDays(daysAfter).ToString("yyyyMMdd");
    }

    public static List<LabelItemDto> GenerateStandardLabels(Item item, string delvedt, string? shptm, int standardBags)
    {
        if (standardBags <= 0) return new List<LabelItemDto>();
        return new List<LabelItemDto>
        {
            new LabelItemDto
            {
                LabelType = "standard",
                Delvedt = delvedt,
                Shptm = shptm,
                Itemcd = item.ItemCd ?? "",
                Itemnm = item.ItemName ?? "",
                ExpiryDate = CalculateExpiryDate(delvedt),
                Strtemp = null,
                Kikunip = null,
                Count = standardBags
            }
        };
    }

    public static List<LabelItemDto> GenerateIrregularLabels(Item item, string delvedt, string? shptm, string shpctrnm, decimal irregularQuantity)
    {
        if (irregularQuantity <= 0) return new List<LabelItemDto>();
        return new List<LabelItemDto>
        {
            new LabelItemDto
            {
                LabelType = "irregular",
                Delvedt = delvedt,
                Shptm = shptm,
                Itemcd = item.ItemCd ?? "",
                Itemnm = item.ItemName ?? "",
                ExpiryDate = CalculateExpiryDate(delvedt),
                Strtemp = null,
                Shpctrnm = shpctrnm,
                IrregularQuantity = irregularQuantity,
                Count = 1
            }
        };
    }

    /// <summary>DTO の値から規格品ラベルを生成（ラベル出力用）</summary>
    public static List<LabelItemDto> GenerateStandardLabelsFromDto(
        string itemcd,
        string itemnm,
        string? strtemp,
        decimal? steritime,
        decimal? kikunip,
        string delvedt,
        string? shptm,
        int standardBags,
        decimal? standardFillQty = null)
    {
        if (standardBags <= 0) return new List<LabelItemDto>();
        return new List<LabelItemDto>
        {
            new LabelItemDto
            {
                LabelType = "standard",
                Delvedt = delvedt,
                Shptm = shptm,
                Itemcd = itemcd,
                Itemnm = itemnm,
                ExpiryDate = CalculateExpiryDate(delvedt),
                Strtemp = strtemp,
                Steritime = steritime,
                Kikunip = kikunip,
                StandardFillQty = standardFillQty,
                Count = standardBags
            }
        };
    }

    /// <summary>DTO の値から端数ラベルを生成（ラベル出力用）</summary>
    public static List<LabelItemDto> GenerateIrregularLabelsFromDto(
        string itemcd,
        string itemnm,
        string? strtemp,
        decimal? steritime,
        string delvedt,
        string? shptm,
        string shpctrnm,
        decimal irregularQuantity)
    {
        if (irregularQuantity <= 0) return new List<LabelItemDto>();
        return new List<LabelItemDto>
        {
            new LabelItemDto
            {
                LabelType = "irregular",
                Delvedt = delvedt,
                Shptm = shptm,
                Itemcd = itemcd,
                Itemnm = itemnm,
                ExpiryDate = CalculateExpiryDate(delvedt),
                Strtemp = strtemp,
                Steritime = steritime,
                Shpctrnm = shpctrnm,
                IrregularQuantity = irregularQuantity,
                Count = 1
            }
        };
    }
}
