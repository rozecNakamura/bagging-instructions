using BaggingInstructions.Api.Core;
using BaggingInstructions.Api.DTOs;
using BaggingInstructions.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace BaggingInstructions.Api.Services;

/// <summary>
/// 個人配送指示書画面用：craftlineaxother.cstmeat を喫食日で検索し、
/// 明細は得意先300・addinfo08先頭1、集計は得意先300/310を対象に喫食日・喫食時間・コースの一覧を返す。
/// </summary>
public class PersonalDeliveryService
{
    private readonly AppDbContext _db;
    private readonly CstmeatDbContext _cstmeatDb;

    public PersonalDeliveryService(AppDbContext db, CstmeatDbContext cstmeatDb)
    {
        _db = db;
        _cstmeatDb = cstmeatDb;
    }

    /// <summary>喫食日（YYYYMMDD）で cstmeat を検索し、喫食日・喫食時間・コースの distinct 一覧を返す。</summary>
    public async Task<List<PersonalDeliverySearchResultDto>> SearchByEatingDateAsync(
        string delvedt,
        string? variant = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(delvedt) || delvedt.Length != 8)
            throw new ArgumentException("喫食日はYYYYMMDD形式（8桁）で指定してください。", nameof(delvedt));

        var isSummary = string.Equals((variant ?? "").Trim(), "summary", StringComparison.OrdinalIgnoreCase);
        var cstmeatRows = await LoadTargetCstmeatRowsAsync(delvedt, isSummary, ct);
        if (cstmeatRows.Count == 0)
            return new List<PersonalDeliverySearchResultDto>();

        var customerCodes = isSummary
            ? PersonalDeliveryHelper.SummaryTargetCustomerCodes
            : new[] { PersonalDeliveryHelper.TargetCustomerCode };

        var locationCodes = cstmeatRows
            .Select(r => (r.Info02 ?? "").Trim())
            .Where(c => c.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var addinfoMap = await LoadLocationAddinfoMapAsync(customerCodes, locationCodes, ct);

        var groups = new HashSet<(string EatingDate, string MealTime, string Course)>();
        foreach (var cm in cstmeatRows)
        {
            var custCode = (cm.Info01 ?? "").Trim();
            var locCode = (cm.Info02 ?? "").Trim();
            if (locCode.Length == 0) continue;

            addinfoMap.TryGetValue((custCode, locCode), out var addinfo);
            if (!isSummary)
            {
                if (addinfo == null) continue;
                if (!PersonalDeliveryHelper.IsTargetAddinfo08(addinfo.Addinfo08)) continue;
            }

            var (course, _) = PersonalDeliveryHelper.ResolveCourseAndOrder(cm.Info19, addinfo);
            groups.Add(((cm.Info03 ?? "").Trim(), (cm.Info04 ?? "").Trim(), course));
        }

        return groups
            .OrderBy(g => g.EatingDate, StringComparer.Ordinal)
            .ThenBy(g => g.MealTime, StringComparer.Ordinal)
            .ThenBy(g => g.Course, StringComparer.Ordinal)
            .Select(g => new PersonalDeliverySearchResultDto
            {
                EatingDate = g.EatingDate,
                MealTime = g.MealTime,
                MealTimeName = BaggingEatingTimeLabel.MapFromAddinfo05(g.MealTime),
                Course = g.Course
            })
            .ToList();
    }

    internal async Task<List<Cstmeat>> LoadTargetCstmeatRowsAsync(string dateStr, bool isSummary, CancellationToken ct)
    {
        var query = _cstmeatDb.Cstmeats
            .AsNoTracking()
            .Where(c => c.Info03 == dateStr);

        if (isSummary)
        {
            var codes = PersonalDeliveryHelper.SummaryTargetCustomerCodes;
            return await query
                .Where(c => c.Info01 != null && codes.Contains(c.Info01.Trim()))
                .ToListAsync(ct);
        }

        return await query
            .Where(c => c.Info01 != null && c.Info01.Trim() == PersonalDeliveryHelper.TargetCustomerCode)
            .ToListAsync(ct);
    }

    internal async Task<Dictionary<(string CustomerCode, string LocationCode), CustomerDeliveryLocationAddinfo>>
        LoadLocationAddinfoMapAsync(
            IReadOnlyList<string> customerCodes,
            IReadOnlyList<string> locationCodes,
            CancellationToken ct)
    {
        if (locationCodes.Count == 0 || customerCodes.Count == 0)
            return new Dictionary<(string, string), CustomerDeliveryLocationAddinfo>();

        var addinfos = await _db.CustomerDeliveryLocationAddinfos
            .AsNoTracking()
            .Where(a => customerCodes.Contains(a.CustomerCode))
            .Where(a => locationCodes.Contains(a.LocationCode))
            .ToListAsync(ct);

        return addinfos
            .GroupBy(a => ((a.CustomerCode ?? "").Trim(), (a.LocationCode ?? "").Trim()))
            .Where(g => g.Key.Item1.Length > 0 && g.Key.Item2.Length > 0)
            .ToDictionary(g => g.Key, g => g.First());
    }
}
