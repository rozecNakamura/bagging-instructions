using BaggingInstructions.Api.DTOs;

namespace BaggingInstructions.Api.Tests;

/// <summary>生産指示 PDF 系テスト用の行モデルビルダー。</summary>
internal static class ProductionInstructionPdfTestModels
{
    internal static ProductionInstructionPdfLineModel ChildLine(
        string orderNo,
        string slot,
        string parentCode,
        string parentName,
        string childCode,
        string childName,
        string qty,
        string unit,
        string yield,
        string spec = "",
        string needDateDisplay = "2025/03/01")
    {
        return new ProductionInstructionPdfLineModel
        {
            OrderNo = orderNo,
            SlotDisplay = slot,
            ParentItemCode = parentCode,
            ParentItemName = parentName,
            PlannedQuantityDisplay = "1",
            PlanUnitName = "kg",
            ChildItemCode = childCode,
            ChildItemName = childName,
            ChildSpec = spec,
            ChildRequiredQtyDisplay = qty,
            ChildUnitName = unit,
            ChildYieldPercentDisplay = yield,
            NeedDateDisplay = needDateDisplay
        };
    }
}
