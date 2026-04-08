using BaggingInstructions.Api.DTOs;

namespace BaggingInstructions.Api.Services;

/// <summary>投入ペイロードを親 BOM の子品目集合と照合する。</summary>
public static class BaggingInputPayloadValidator
{
    /// <summary>
    /// BOM に1件以上ある場合、各行の <see cref="BaggingInputLineDto.Citemcd"/> は BOM の子品目に含まれること。
    /// BOM が無い場合は検証しない（マスタ未登録品など）。
    /// </summary>
    public static void ValidateLinesAgainstBom(
        IReadOnlyCollection<string> bomChildItemCodes,
        BaggingInputPayloadDto? payload)
    {
        if (payload?.Lines == null || payload.Lines.Count == 0)
            return;

        var allowed = new HashSet<string>(
            bomChildItemCodes.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()),
            StringComparer.OrdinalIgnoreCase);

        if (allowed.Count == 0)
            return;

        foreach (var line in payload.Lines)
        {
            var c = (line.Citemcd ?? "").Trim();
            if (string.IsNullOrEmpty(c))
                throw new ArgumentException("子品目コードが空の行があります。登録できません。");

            if (!allowed.Contains(c))
                throw new ArgumentException($"BOM にない子品目コードが含まれています: {c}");
        }
    }
}
