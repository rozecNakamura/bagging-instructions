using System.Globalization;
using BaggingInstructions.Api.DTOs;

namespace BaggingInstructions.Api.Services;

/// <summary>袋詰現品票1枚.rxz：LabelItemDto から1ページ1ラベルのPDFを生成。ordertable 不要。</summary>
public sealed class BaggingLabelPdfService
{
    private static readonly IReadOnlyDictionary<string, int> AlignmentOverrides =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["DATE01"]                    = 1,
            ["TIME01"]                    = 1,
            ["PAGENO"]                    = 2,
            ["CUSTOMERDELIVERYLOCATION01"] = 1,
            ["ITEMNO01"]                  = 1,
            ["ITEMNM01"]                  = 1,
            ["CLASSIFICATION1NAME01"]     = 1,
            ["STERITEMPRANGE01"]          = 1,
            ["CHILDITEMNO01"]             = 1,
            ["CHILDITEMNM01"]             = 1,
            ["QUANTITY01"]                = 2,
            ["UNIT01"]                    = 1,
            ["BBDT01"]                    = 1,
        };

    // テンプレートのフォントサイズが固定のため、長い値でも枠内に収まるよう ShrinkToFit を強制するフィールド
    private static readonly IReadOnlySet<string> ShrinkToFitOverrides =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "DATE01",
            "TIME01",
            "CUSTOMERDELIVERYLOCATION01",
            "ITEMNM01",
            "CLASSIFICATION1NAME01",
        };

    private readonly JuicePdfService _juicePdf;

    public BaggingLabelPdfService(JuicePdfService juicePdf)
    {
        _juicePdf = juicePdf;
    }

    public byte[] GeneratePdf(string rxzTemplatePath, IReadOnlyList<LabelItemDto> items)
    {
        var pages = new List<Dictionary<string, string>>();
        foreach (var item in items)
        {
            var pageCount = item.LabelType == "irregular" ? 1 : Math.Max(1, item.Count);
            for (var i = 0; i < pageCount; i++)
                pages.Add(BuildPageTags(item, i));
        }
        if (pages.Count == 0) return Array.Empty<byte>();
        return _juicePdf.GeneratePdfMultiPage(rxzTemplatePath, pages, "袋詰現品票", AlignmentOverrides, ShrinkToFitOverrides);
    }

    private static Dictionary<string, string> BuildPageTags(LabelItemDto item, int pageIndex)
    {
        var qty = item.LabelType == "irregular"
            ? FormatQty(item.IrregularQuantity ?? 0)
            : FormatQty(item.StandardFillQty ?? item.Kikunip ?? 0);

        var pageNoStr = item.PageNo > 0
            ? $"{item.StartPageNo + pageIndex}/{item.PageNo}"
            : "";

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["DATE01"]                    = FormatDate(item.Delvedt),
            ["TIME01"]                    = item.ShptmName ?? "",
            ["PAGENO"]                    = pageNoStr,
            ["CUSTOMERDELIVERYLOCATION01"] = item.Shpctrnm ?? "",
            ["ITEMNO01"]                  = item.Itemcd,
            ["ITEMNM01"]                  = item.Itemnm,
            ["CLASSIFICATION1NAME01"]     = item.Classification1Name ?? "",
            ["STERITEMPRANGE01"]          = item.Strtemp ?? "",
            ["CHILDITEMNO01"]             = "",
            ["CHILDITEMNM01"]             = item.Strtemp ?? "",
            ["QUANTITY01"]                = qty,
            ["UNIT01"]                    = item.UnitName ?? "",
            ["BBDT01"]                    = FormatDate(item.ExpiryDate),
        };
    }

    private static string FormatDate(string? yyyymmdd)
    {
        if (string.IsNullOrEmpty(yyyymmdd) || yyyymmdd.Length != 8) return yyyymmdd ?? "";
        return $"{yyyymmdd[..4]}/{yyyymmdd[4..6]}/{yyyymmdd[6..]}";
    }

    private static string FormatQty(decimal q) =>
        q == 0 ? "" : q.ToString("0.######", CultureInfo.InvariantCulture);
}
