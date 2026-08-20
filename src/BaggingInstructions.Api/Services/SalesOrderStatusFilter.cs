using System.Linq.Expressions;
using BaggingInstructions.Api.Entities;

namespace BaggingInstructions.Api.Services;

/// <summary>
/// 受注明細ステータスの検索対象判定。
/// 確定/未確定は受注ヘッダ（salesorder.status）ではなく明細（salesorderline.status）で判断する。
/// ただし受注ヘッダが取消（cancelled）の場合は明細に関わらず対象外とする。
/// 通常は確定（confirmed）分のみを対象とするが、salesorderlineaddinfo.addinfo07='order2' は
/// 運用上そもそも確定処理を行わず予定のまま残るため、取消（cancelled）以外なら対象に含める。
/// また、同一受注内に確定（confirmed）明細が 1 件も無い場合は、未処理（open）明細も対象に含める
/// （明細単位の確定処理が行われていない受注を取りこぼさないため）。
/// </summary>
public static class SalesOrderStatusFilter
{
    public const string StatusConfirmed = "confirmed";
    public const string StatusCancelled = "cancelled";
    public const string StatusOpen = "open";
    public const string Order2 = "order2";

    /// <summary>addinfo07 が order2（確定処理を行わない受注）か。</summary>
    public static bool IsOrder2(string? addinfo07) =>
        string.Equals((addinfo07 ?? "").Trim(), Order2, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 受注明細が確定分、order2（取消以外）、または確定明細が 1 件も無い受注の open 明細か。
    /// 受注ヘッダが取消の場合は常に対象外。IQueryable にそのまま渡せる。
    /// </summary>
    public static Expression<Func<SalesOrderLine, bool>> ConfirmedOrOrder2Line => l =>
        l.SalesOrder != null
        && l.SalesOrder.Status != StatusCancelled
        && (l.Status == StatusConfirmed
            || (l.Status != StatusCancelled
                && l.Addinfo != null
                && l.Addinfo.Addinfo07 != null
                && l.Addinfo.Addinfo07.Trim().ToLower() == Order2)
            || (l.Status == StatusOpen
                && !l.SalesOrder.SalesOrderLines.Any(x => x.Status == StatusConfirmed)));

    /// <summary>
    /// 取消以外の受注で、確定分・order2（取消以外）・open のいずれかの明細を 1 件以上持つか。
    /// open 明細は「確定明細が無い場合のみ対象」だが、確定明細があれば別条件で成立するため単純な OR で足りる。
    /// IQueryable にそのまま渡せる。
    /// </summary>
    public static Expression<Func<SalesOrder, bool>> ConfirmedOrOrder2Order => so =>
        so.Status != StatusCancelled
        && so.SalesOrderLines.Any(l =>
            l.Status == StatusConfirmed
            || l.Status == StatusOpen
            || (l.Status != StatusCancelled
                && l.Addinfo != null
                && l.Addinfo.Addinfo07 != null
                && l.Addinfo.Addinfo07.Trim().ToLower() == Order2));
}
