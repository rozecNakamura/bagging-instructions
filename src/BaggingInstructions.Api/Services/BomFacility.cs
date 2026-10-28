namespace BaggingInstructions.Api.Services;

/// <summary>
/// BOM 取得時の工場コード解決。
/// 製造オーダーの工場コード（<c>ordertable.facilitycode</c>）を優先し、未設定なら受注明細の工場コード
/// （<c>salesorderline.facilitycode</c>）、それも未設定（NULL/空）なら <see cref="Default"/>（MATSUYAMA）の BOM を使用する。
/// </summary>
public static class BomFacility
{
    /// <summary>製造オーダー・受注明細のいずれにも工場コードが無い場合の既定工場。</summary>
    public const string Default = "MATSUYAMA";

    /// <summary>工場コードから、BOM 取得に使う工場コードを決定する。</summary>
    public static string Resolve(string? facilityCode)
    {
        var v = facilityCode?.Trim();
        return string.IsNullOrEmpty(v) ? Default : v;
    }

    /// <summary>製造オーダー → 受注明細の順で、BOM 取得に使う工場コードを決定する。</summary>
    public static string Resolve(string? orderFacilityCode, string? salesOrderLineFacilityCode)
    {
        var v = orderFacilityCode?.Trim();
        return string.IsNullOrEmpty(v) ? Resolve(salesOrderLineFacilityCode) : v;
    }

    /// <summary>
    /// SQL 内で受注明細の工場コードから BOM 側の工場コードを解決する式。
    /// 引数には salesorderline のエイリアス付き列（例: <c>sol.facilitycode</c>）を渡す。
    /// </summary>
    public static string SqlResolve(string salesOrderLineFacilityColumn)
        => $"COALESCE(NULLIF(TRIM(BOTH FROM {salesOrderLineFacilityColumn}), ''), '{Default}')";

    /// <summary>
    /// SQL 内で製造オーダー → 受注明細の順に BOM 側の工場コードを解決する式。
    /// 引数には ordertable / salesorderline のエイリアス付き列（例: <c>ot.facilitycode</c>, <c>sol.facilitycode</c>）を渡す。
    /// </summary>
    public static string SqlResolve(string orderFacilityColumn, string salesOrderLineFacilityColumn)
        => $"COALESCE(NULLIF(TRIM(BOTH FROM {orderFacilityColumn}), ''), NULLIF(TRIM(BOTH FROM {salesOrderLineFacilityColumn}), ''), '{Default}')";
}
