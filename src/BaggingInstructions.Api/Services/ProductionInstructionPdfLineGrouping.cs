using BaggingInstructions.Api.DTOs;

namespace BaggingInstructions.Api.Services;

/// <summary>
/// 調味液系・生産指示書 PDF 共通：オーダー単位グループ化と便・注番での並び。
/// </summary>
internal static class ProductionInstructionPdfLineGrouping
{
    internal static List<List<ProductionInstructionPdfLineModel>> GroupByOrderSortedBySlotThenOrderId(
        IReadOnlyList<ProductionInstructionPdfLineModel> lines)
    {
        return lines
            .GroupBy(l => l.OrderNo ?? "", StringComparer.Ordinal)
            .Select(g => g.ToList())
            .OrderBy(g => g[0].SlotDisplay ?? "", StringComparer.Ordinal)
            .ThenBy(g => ParseOrderKey(g[0].OrderNo))
            .ToList();
    }

    internal static long ParseOrderKey(string? orderNo)
    {
        if (long.TryParse(orderNo, out var id))
            return id;
        return 0;
    }
}
