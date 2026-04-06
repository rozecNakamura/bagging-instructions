using BaggingInstructions.Api.DTOs;

namespace BaggingInstructions.Api.Services;

/// <summary>
/// 生産指示書系 RXZ への数量・品名テキスト整形（ホイコーロー・がんも等で共用）。
/// </summary>
internal static class ProductionInstructionRxzTagTexts
{
    internal static string SpecOrItemName(ProductionInstructionPdfLineModel row) =>
        string.IsNullOrWhiteSpace(row.ChildSpec) ? row.ChildItemName ?? "" : row.ChildSpec;

    internal static string FormatQtyWithUnit(string? qty, string? unit)
    {
        var q = (qty ?? "").Trim();
        var u = (unit ?? "").Trim();
        if (q.Length == 0)
            return "";
        return u.Length == 0 ? q : $"{q} {u}";
    }
}
