namespace BaggingInstructions.Api.Core;

/// <summary>
/// 共通で使用する SQL 断片。
/// </summary>
public static class SqlFragments
{
    /// <summary>
    /// itemcode から作業区名を集約するサブクエリ。
    /// <paramref name="itemCodeExpression"/> には SQL 上の itemcode 式（例: <c>i.itemcode</c>）を渡す。
    /// </summary>
    public static string WorkplaceNamesByItemcode(string itemCodeExpression) =>
        $@"
SELECT string_agg(DISTINCT wc.workcentername, '、' ORDER BY wc.workcentername)
FROM itemworkcentermapping m2
INNER JOIN workcenter wc ON wc.workcentercode = m2.workcentercode
WHERE m2.itemcode = {itemCodeExpression}";
}

