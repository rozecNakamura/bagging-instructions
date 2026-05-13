using BaggingInstructions.Api.Entities;
using BaggingInstructions.Api.DTOs;

namespace BaggingInstructions.Api.Services;

public static class LabelGeneratorService
{
    public const int DefaultDaysAfter = 3;

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
        decimal? kikunip,
        string delvedt,
        string? shptm,
        int standardBags,
        decimal? standardFillQty = null,
        string? expiryDateOverride = null,
        string? unitName = null,
        string? shptmName = null,
        string? shpctrnm = null,
        string? classification1Name = null,
        int pageNo = 0,
        int startPageNo = 1)
    {
        if (standardBags <= 0) return new List<LabelItemDto>();
        return new List<LabelItemDto>
        {
            new LabelItemDto
            {
                LabelType = "standard",
                Delvedt = delvedt,
                Shptm = shptm,
                ShptmName = shptmName,
                Itemcd = itemcd,
                Itemnm = itemnm,
                ExpiryDate = !string.IsNullOrEmpty(expiryDateOverride) ? expiryDateOverride : CalculateExpiryDate(delvedt),
                Strtemp = strtemp,
                Kikunip = kikunip,
                StandardFillQty = standardFillQty,
                Count = standardBags,
                UnitName = unitName,
                Shpctrnm = shpctrnm,
                Classification1Name = classification1Name,
                PageNo = pageNo,
                StartPageNo = startPageNo
            }
        };
    }

    /// <summary>DTO の値から端数ラベルを生成（ラベル出力用）</summary>
    public static List<LabelItemDto> GenerateIrregularLabelsFromDto(
        string itemcd,
        string itemnm,
        string? strtemp,
        string delvedt,
        string? shptm,
        string shpctrnm,
        decimal irregularQuantity,
        string? expiryDateOverride = null,
        string? unitName = null,
        string? shptmName = null,
        string? classification1Name = null,
        int pageNo = 0,
        int startPageNo = 1)
    {
        if (irregularQuantity <= 0) return new List<LabelItemDto>();
        return new List<LabelItemDto>
        {
            new LabelItemDto
            {
                LabelType = "irregular",
                Delvedt = delvedt,
                Shptm = shptm,
                ShptmName = shptmName,
                Itemcd = itemcd,
                Itemnm = itemnm,
                ExpiryDate = !string.IsNullOrEmpty(expiryDateOverride) ? expiryDateOverride : CalculateExpiryDate(delvedt),
                Strtemp = strtemp,
                Shpctrnm = shpctrnm,
                IrregularQuantity = irregularQuantity,
                Count = 1,
                UnitName = unitName,
                Classification1Name = classification1Name,
                PageNo = pageNo,
                StartPageNo = startPageNo
            }
        };
    }
}
