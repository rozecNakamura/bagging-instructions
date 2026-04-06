using BaggingInstructions.Api.DTOs;

namespace BaggingInstructions.Api.Services;

/// <summary>
/// 生産指示系 RXZ 帳票で共通する「オーダー単位グループ → BOM 子行をページサイズで分割 → タグ辞書一覧」。
/// </summary>
internal static class ProductionInstructionRxzPaging
{
    internal static List<Dictionary<string, string>> BuildMultiPageTagDictionaries(
        IReadOnlyList<ProductionInstructionPdfLineModel> lines,
        int maxMaterialsPerPage,
        Func<ProductionInstructionPdfLineModel, IReadOnlyList<ProductionInstructionPdfLineModel>, Dictionary<string, string>>
            buildPageTags)
    {
        if (lines == null || lines.Count == 0)
            return new List<Dictionary<string, string>>();

        var groups = ProductionInstructionPdfLineGrouping.GroupByOrderSortedBySlotThenOrderId(lines);
        var pageTagList = new List<Dictionary<string, string>>();

        foreach (var grp in groups)
        {
            var header = grp[0];
            var childRows = grp.Where(r => !string.IsNullOrWhiteSpace(r.ChildItemCode)).ToList();
            if (childRows.Count == 0)
            {
                pageTagList.Add(buildPageTags(header, Array.Empty<ProductionInstructionPdfLineModel>()));
                continue;
            }

            for (var off = 0; off < childRows.Count; off += maxMaterialsPerPage)
            {
                var chunk = childRows.Skip(off).Take(maxMaterialsPerPage).ToList();
                pageTagList.Add(buildPageTags(header, chunk));
            }
        }

        return pageTagList;
    }
}
