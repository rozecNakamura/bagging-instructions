namespace BaggingInstructions.Api.Services;

/// <summary>
/// BOM 取得時の工場コード解決。
/// bom.facilitycode = salesorderline.facilitycode で絞り込み、受注明細側が未設定（NULL/空）の場合は
/// <see cref="Default"/>（MATSUYAMA）の BOM を使用する。
/// </summary>
public static class BomFacility
{
    /// <summary>受注明細に工場コードが無い場合の既定工場。</summary>
    public const string Default = "MATSUYAMA";

    /// <summary>受注明細の工場コードから、BOM 取得に使う工場コードを決定する。</summary>
    public static string Resolve(string? salesOrderLineFacilityCode)
    {
        var v = salesOrderLineFacilityCode?.Trim();
        return string.IsNullOrEmpty(v) ? Default : v;
    }

    /// <summary>
    /// SQL 内で受注明細の工場コードから BOM 側の工場コードを解決する式。
    /// 引数には salesorderline のエイリアス付き列（例: <c>sol.facilitycode</c>）を渡す。
    /// </summary>
    public static string SqlResolve(string salesOrderLineFacilityColumn)
        => $"COALESCE(NULLIF(TRIM(BOTH FROM {salesOrderLineFacilityColumn}), ''), '{Default}')";
}
