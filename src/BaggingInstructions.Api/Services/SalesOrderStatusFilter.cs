using System.Linq.Expressions;
using BaggingInstructions.Api.Entities;

namespace BaggingInstructions.Api.Services;

/// <summary>
/// 受注ステータスの検索対象判定。
/// 通常は確定（confirmed）分のみを対象とするが、salesorderlineaddinfo.addinfo07='order2' は
/// 運用上そもそも確定処理を行わず予定のまま残るため、取消（cancelled）以外なら対象に含める。
/// </summary>
public static class SalesOrderStatusFilter
{
    public const string StatusConfirmed = "confirmed";
    public const string StatusCancelled = "cancelled";
    public const string Order2 = "order2";

    /// <summary>addinfo07 が order2（確定処理を行わない受注）か。</summary>
    public static bool IsOrder2(string? addinfo07) =>
        string.Equals((addinfo07 ?? "").Trim(), Order2, StringComparison.OrdinalIgnoreCase);

    /// <summary>受注明細が確定分、または order2（取消以外）か。IQueryable にそのまま渡せる。</summary>
    public static Expression<Func<SalesOrderLine, bool>> ConfirmedOrOrder2Line => l =>
        l.SalesOrder != null
        && (l.SalesOrder.Status == StatusConfirmed
            || (l.SalesOrder.Status != StatusCancelled
                && l.Addinfo != null
                && l.Addinfo.Addinfo07 != null
                && l.Addinfo.Addinfo07.Trim().ToLower() == Order2));

    /// <summary>受注が確定分、または order2 明細を持つ取消以外の受注か。IQueryable にそのまま渡せる。</summary>
    public static Expression<Func<SalesOrder, bool>> ConfirmedOrOrder2Order => so =>
        so.Status == StatusConfirmed
        || (so.Status != StatusCancelled
            && so.SalesOrderLines.Any(l =>
                l.Addinfo != null
                && l.Addinfo.Addinfo07 != null
                && l.Addinfo.Addinfo07.Trim().ToLower() == Order2));
}
