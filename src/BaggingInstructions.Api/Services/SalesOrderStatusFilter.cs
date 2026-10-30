using System.Linq.Expressions;
using BaggingInstructions.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace BaggingInstructions.Api.Services;

/// <summary>
/// 受注明細ステータスの検索対象判定。
/// 確定/未確定は受注ヘッダ（salesorder.status）ではなく明細（salesorderline.status）で判断する。
/// ただし受注ヘッダが取消（cancelled）の場合は明細に関わらず対象外とする。
/// 通常は確定（confirmed）分のみを対象とするが、salesorderlineaddinfo.addinfo07='order2' は
/// 運用上そもそも確定処理を行わず予定のまま残るため、取消（cancelled）以外なら対象に含める。
/// また、同一受注内に確定（confirmed）明細が 1 件も無い場合は、未処理（open）明細も対象に含める
/// （明細単位の確定処理が行われていない受注を取りこぼさないため）。
/// ただし同一の食事（得意先・納入場所・納品日・喫食時間・食種）に確定明細が別受注で存在する open 明細は
/// 確定取込で取消されたものとして <see cref="ExcludeCancelledOpenLines"/> で除く。
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
    /// <see cref="ConfirmedOrOrder2Line"/> で読み込んだ明細から、確定取込で取消された open 明細を除く。
    /// 予定取込の明細のうち確定取込に含まれなかったもの（例: 御飯のみ取消）は open のまま残るが、
    /// 受注が明細ごとに分かれているため同一受注内の確定明細の有無では判定できない。
    /// 同一の得意先・納入場所・納品日・喫食時間（addinfo05）・食種（addinfo02）の確定明細が別受注にあれば、
    /// その open 明細（order2 以外）は取消されたものとみなす。
    /// SQL の相関サブクエリにすると明細ごとに再実行され遅いため、確定明細のキーを 1 回で取得して突合する。
    /// <paramref name="allLines"/> には DbContext.SalesOrderLines を渡す。
    /// </summary>
    public static List<SalesOrderLine> ExcludeCancelledOpenLines(
        IQueryable<SalesOrderLine> allLines,
        List<SalesOrderLine> lines)
    {
        var query = BuildConfirmedMealKeyQuery(allLines, lines);
        return query == null ? lines : ExcludeLinesWithConfirmedMeal(lines, query.ToList());
    }

    /// <inheritdoc cref="ExcludeCancelledOpenLines"/>
    public static async Task<List<SalesOrderLine>> ExcludeCancelledOpenLinesAsync(
        IQueryable<SalesOrderLine> allLines,
        List<SalesOrderLine> lines,
        CancellationToken ct = default)
    {
        var query = BuildConfirmedMealKeyQuery(allLines, lines);
        return query == null ? lines : ExcludeLinesWithConfirmedMeal(lines, await query.ToListAsync(ct));
    }

    /// <summary>確定明細の食事キーと一致する open 明細（order2 以外）を除く。</summary>
    internal static List<SalesOrderLine> ExcludeLinesWithConfirmedMeal(
        List<SalesOrderLine> lines,
        IEnumerable<ConfirmedMealKey> confirmedKeys)
    {
        var keySet = confirmedKeys.ToHashSet();
        if (keySet.Count == 0) return lines;
        return lines
            .Where(l => !IsCancellableOpenLine(l) || !keySet.Contains(MealKeyOf(l)))
            .ToList();
    }

    /// <summary>確定取込で取消された可能性のある open 明細か（order2 は確定処理を行わないため除く）。</summary>
    private static bool IsCancellableOpenLine(SalesOrderLine l) =>
        l.Status == StatusOpen
        && l.SalesOrder != null
        && l.Addinfo != null
        && l.PlannedDeliveryDate.HasValue
        && !IsOrder2(l.Addinfo.Addinfo07);

    private static ConfirmedMealKey MealKeyOf(SalesOrderLine l) => new(
        (l.SalesOrder?.CustomerCode ?? "").Trim(),
        (l.SalesOrder?.CustomerDeliveryLocationCode ?? "").Trim(),
        l.PlannedDeliveryDate,
        (l.Addinfo?.Addinfo05 ?? "").Trim(),
        (l.Addinfo?.Addinfo02 ?? "").Trim());

    /// <summary>取消判定が必要な open 明細と同じ納品日範囲・得意先・納入場所の確定明細のキーを取得するクエリ。対象が無ければ null。</summary>
    private static IQueryable<ConfirmedMealKey>? BuildConfirmedMealKeyQuery(
        IQueryable<SalesOrderLine> allLines,
        List<SalesOrderLine> lines)
    {
        var targets = lines.Where(IsCancellableOpenLine).ToList();
        if (targets.Count == 0) return null;

        var minDate = targets.Min(l => l.PlannedDeliveryDate);
        var maxDate = targets.Max(l => l.PlannedDeliveryDate);
        var customerCodes = targets.Select(l => l.SalesOrder!.CustomerCode ?? "").Distinct().ToList();
        var locationCodes = targets.Select(l => l.SalesOrder!.CustomerDeliveryLocationCode ?? "").Distinct().ToList();

        return allLines
            .Where(x => x.Status == StatusConfirmed
                && x.PlannedDeliveryDate >= minDate
                && x.PlannedDeliveryDate <= maxDate
                && x.SalesOrder != null
                && x.SalesOrder.Status != StatusCancelled
                && x.Addinfo != null
                && customerCodes.Contains(x.SalesOrder.CustomerCode ?? "")
                && locationCodes.Contains(x.SalesOrder.CustomerDeliveryLocationCode ?? ""))
            .Select(x => new ConfirmedMealKey(
                (x.SalesOrder.CustomerCode ?? "").Trim(),
                (x.SalesOrder.CustomerDeliveryLocationCode ?? "").Trim(),
                x.PlannedDeliveryDate,
                (x.Addinfo!.Addinfo05 ?? "").Trim(),
                (x.Addinfo.Addinfo02 ?? "").Trim()));
    }

    /// <summary>確定明細の有無を判定する食事単位のキー（得意先・納入場所・納品日・喫食時間・食種）。</summary>
    internal readonly record struct ConfirmedMealKey(
        string CustomerCode,
        string LocationCode,
        DateOnly? DeliveryDate,
        string MealTime,
        string FoodType);

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
