using BaggingInstructions.Api.Core;
using BaggingInstructions.Api.DTOs;
using Microsoft.EntityFrameworkCore;

namespace BaggingInstructions.Api.Services;

/// <summary>
/// 個人配送指示書画面用：salesorderline を配送日（planneddeliverydate）で検索し、
/// 配送日・喫食時間（salesorderlineaddinfo.addinfo01name）・配送エリア（customerdeliverylocationaddinfo.addinfo01）の一覧を返す。
/// 配送エリア未設定（null/空）の組み合わせは検索結果に含めない。
/// </summary>
public class PersonalDeliveryService
{
    private readonly AppDbContext _db;

    public PersonalDeliveryService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>配送日（YYYYMMDD）で salesorderline を検索し、配送エリアが設定されている組み合わせに限り、配送日・喫食時間・配送エリアの distinct 一覧を返す。</summary>
    public async Task<List<PersonalDeliverySearchResultDto>> SearchByDeliveryDateAsync(string delvedt, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(delvedt) || delvedt.Length != 8)
            throw new ArgumentException("配送日はYYYYMMDD形式（8桁）で指定してください。", nameof(delvedt));

        if (!DateOnly.TryParseExact(delvedt, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var deliveryDate))
            throw new ArgumentException("配送日の形式が不正です。", nameof(delvedt));

        // DateOnly.ToString は SQL に変換されないため、DB では日付をそのまま取得し、メモリ上でフォーマットする
        var rows = await _db.SalesOrderLines
            .AsNoTracking()
            .Include(l => l.Addinfo)
            .Include(l => l.SalesOrder!)
                .ThenInclude(so => so!.CustomerDeliveryLocation)
            .Where(l => l.PlannedDeliveryDate == deliveryDate)
            .Select(l => new
            {
                PlannedDeliveryDate = l.PlannedDeliveryDate,
                TimeName = l.Addinfo != null ? l.Addinfo.Addinfo01Name : null,
                Area = l.SalesOrder != null && l.SalesOrder.CustomerDeliveryLocation != null && l.SalesOrder.CustomerDeliveryLocation.Addinfo != null
                    ? l.SalesOrder.CustomerDeliveryLocation.Addinfo.Addinfo01
                    : null
            })
            .Where(x => x.TimeName != null || x.Area != null)
            .ToListAsync(ct);

        return rows
            .Select(r => new
            {
                DeliveryDate = r.PlannedDeliveryDate.HasValue ? r.PlannedDeliveryDate!.Value.ToString("yyyyMMdd") : "",
                TimeName = r.TimeName,
                Area = r.Area
            })
            .Where(x => x.DeliveryDate != "" && !string.IsNullOrEmpty(x.Area))
            .Distinct()
            .OrderBy(x => x.DeliveryDate)
            .ThenBy(x => x.TimeName ?? "")
            .ThenBy(x => x.Area ?? "")
            .Select(r => new PersonalDeliverySearchResultDto
            {
                DeliveryDate = r.DeliveryDate,
                TimeName = r.TimeName ?? "",
                Area = r.Area ?? ""
            })
            .ToList();
    }
}
