using System.Data;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using BaggingInstructions.Api.Core;
using BaggingInstructions.Api.Entities;
using BaggingInstructions.Api.DTOs;
using BaggingInstructions.Api.QueryResults;

namespace BaggingInstructions.Api.Services;

public class SearchService
{
    private readonly AppDbContext _db;
    private readonly CstmeatDbContext _otherDb;

    public SearchService(AppDbContext db, CstmeatDbContext otherDb)
    {
        _db = db;
        _otherDb = otherDb;
    }

    /// <summary>受注明細を検索（基本情報のみ）。productdate で検索、itemcd は部分一致。</summary>
    public async Task<List<JobordItemDto>> SearchAsync(string prddt, string? itemcd, CancellationToken ct = default)
    {
        var prddtDate = ParseProductDate(prddt);
        if (!prddtDate.HasValue)
            throw new ArgumentException("製造日はYYYYMMDD形式（8桁）で指定してください。", nameof(prddt));
        var query = _db.SalesOrderLines.AsNoTracking()
            .Include(l => l.SalesOrder!)
                .ThenInclude(so => so!.Customer)
            .Include(l => l.SalesOrder!)
                .ThenInclude(so => so!.CustomerDeliveryLocation)
            .Include(l => l.Addinfo)
            .Include(l => l.Item)
            .Where(l => l.ProductDate == prddtDate);

        if (!string.IsNullOrEmpty(itemcd))
            query = query.Where(l => l.Item != null && l.Item.ItemCd != null && l.Item.ItemCd.Contains(itemcd));

        var lines = await query
            .OrderBy(l => l.SalesOrderLineId)
            .ToListAsync(ct);

        return lines.Select(l => new JobordItemDto
        {
            Prkey = l.SalesOrderLineId,
            Prddt = l.ProductDate.HasValue ? l.ProductDate.Value.ToString("yyyyMMdd") : null,
            Delvedt = l.PlannedDeliveryDate.HasValue ? l.PlannedDeliveryDate.Value.ToString("yyyyMMdd") : null,
            Shptm = l.Addinfo != null ? l.Addinfo.Addinfo04 : null,
            Cuscd = l.SalesOrder != null && l.SalesOrder.Customer != null ? l.SalesOrder.Customer.CustomerCode : null,
            Shpctrcd = l.SalesOrder != null && l.SalesOrder.CustomerDeliveryLocation != null ? l.SalesOrder.CustomerDeliveryLocation.LocationCode : null,
            Itemcd = l.Item != null ? l.Item.ItemCd : null,
            Jobordmernm = l.Item != null ? l.Item.ItemName : null,
            Jobordqun = l.Quantity
        }).ToList();
    }

    /// <summary>袋詰用：品目コードが 40 で始まる受注明細を製造日・品目コードで検索し、製造日×品目で合算したグループを返す。</summary>
    public async Task<List<BaggingSearchGroupDto>> SearchBaggingGroupedAsync(string prddt, string? itemcd, bool? isComplete = null, CancellationToken ct = default)
    {
        var prddtDate = ParseProductDate(prddt);
        if (!prddtDate.HasValue)
            throw new ArgumentException("製造日はYYYYMMDD形式（8桁）で指定してください。", nameof(prddt));

        var query = _db.SalesOrderLines.AsNoTracking()
            .Include(l => l.Item!)
                .ThenInclude(i => i!.Unit0)
            .Include(l => l.SalesOrder)
            .Where(l => l.ProductDate == prddtDate)
            .Where(l => l.Item != null && l.Item.ItemCd != null && l.Item.ItemCd.StartsWith("40"));

        if (!string.IsNullOrEmpty(itemcd))
            query = query.Where(l => l.Item != null && l.Item.ItemCd != null && l.Item.ItemCd.Contains(itemcd));

        var lines = await query
            .OrderBy(l => l.SalesOrderLineId)
            .ToListAsync(ct);

        var filteredLines = lines
            .Where(l => l.Item != null && !string.IsNullOrEmpty(l.Item.ItemCd))
            .ToList();

        var printStatusRows = await _otherDb.BaggedQuantities.AsNoTracking()
            .Where(r => r.ProductDate == prddtDate.Value)
            .GroupBy(r => r.ParentItemCode)
            .Select(g => new
            {
                ParentItemCode = g.Key,
                IsPrinted = g.Any(r => r.IsPrinted),
                IsInstructionPrinted = g.Any(r => r.IsInstructionPrinted),
                IsLabelPrinted = g.Any(r => r.IsLabelPrinted)
            })
            .ToListAsync(ct);
        var printStatusByCode = printStatusRows.ToDictionary(r => r.ParentItemCode, StringComparer.Ordinal);

        var groups = filteredLines
            .GroupBy(l => l.Item!.ItemCd!)
            .Select(g =>
            {
                var first = g.First();
                var item = first.Item!;
                printStatusByCode.TryGetValue(g.Key, out var ps);
                return new BaggingSearchGroupDto
                {
                    Prddt = prddtDate.Value.ToString("yyyyMMdd"),
                    Itemcd = g.Key,
                    Itemnm = item.ItemName,
                    TotalJobordqun = g.Sum(x => x.Quantity),
                    UnitCode = item.Unit0?.UnitCode,
                    UnitName = item.Unit0?.UnitName,
                    LinePrkeys = g.Select(x => x.SalesOrderLineId).OrderBy(id => id).ToList(),
                    IsPrinted = ps?.IsPrinted ?? false,
                    IsInstructionPrinted = ps?.IsInstructionPrinted ?? false,
                    IsLabelPrinted = ps?.IsLabelPrinted ?? false
                };
            })
            .OrderBy(x => x.Itemcd, StringComparer.Ordinal)
            .ToList();

        if (isComplete.HasValue)
            groups = groups.Where(g => g.IsPrinted == isComplete.Value).ToList();

        return groups;
    }

    /// <summary>汁仕分表用：喫食日・品目コードで検索。delvedt は YYYYMMDD、itemcd は部分一致。item.middleclassficationcode が 50 または 51 の品目のみ。</summary>
    public async Task<List<JobordItemDto>> SearchByDeliveryDateAsync(string delvedt, string? itemcd, string? mealTime = null, CancellationToken ct = default)
    {
        var delvedtDate = ParseProductDate(delvedt);
        if (!delvedtDate.HasValue)
            throw new ArgumentException("喫食日はYYYYMMDD形式（8桁）で指定してください。", nameof(delvedt));
        var query = _db.SalesOrderLines.AsNoTracking()
            .Include(l => l.SalesOrder!)
                .ThenInclude(so => so!.Customer)
            .Include(l => l.SalesOrder!)
                .ThenInclude(so => so!.CustomerDeliveryLocation)
            .Include(l => l.Addinfo)
            .Include(l => l.Item)
            .Where(l => l.PlannedDeliveryDate == delvedtDate)
            .Where(l => l.SalesOrder != null
                && (l.SalesOrder.CustomerCode == "200" || l.SalesOrder.CustomerCode == "210"))
            .Where(l => l.Item != null
                && l.Item.ItemCd != null
                && (l.Item.ItemCd.StartsWith("3050") || l.Item.ItemCd.StartsWith("3051")
                    || l.Item.ItemCd.StartsWith("3150") || l.Item.ItemCd.StartsWith("3151")));

        if (!string.IsNullOrEmpty(itemcd))
            query = query.Where(l => l.Item != null && l.Item.ItemCd != null && l.Item.ItemCd.Contains(itemcd));

        var mealTimeStr = (mealTime ?? "").Trim();
        if (mealTimeStr.Length > 0)
            query = query.Where(l => l.Addinfo != null && l.Addinfo.Addinfo05 == mealTimeStr);

        var linesJuice = await query
            .OrderBy(l => l.SalesOrderLineId)
            .ToListAsync(ct);

        var cstmeatMap = await LoadCstmeatQuantityMapAsync(delvedtDate.Value, ct);

        return linesJuice.Select(l =>
        {
            var cuscd = (l.SalesOrder?.Customer?.CustomerCode ?? "").Trim();
            var shpctrcd = (l.SalesOrder?.CustomerDeliveryLocation?.LocationCode ?? "").Trim();
            var mealTimeKey = (l.Addinfo?.Addinfo05 ?? "").Trim();
            var foodType = (l.Addinfo?.Addinfo02 ?? "").Trim();
            decimal jobordqun;
            if (cstmeatMap.TryGetValue((cuscd, shpctrcd, mealTimeKey, foodType), out var cstQty))
            {
                // cstmeat の食数（cstQty）× addinfo01（1人あたりグラム）= 総グラム数として設定。
                // PDF の PACK = GRAM / addinfo01 = cstQty（食数）になる。
                var addinfo01Dec = TryParseAddinfoDivisor(l.Addinfo?.Addinfo01);
                jobordqun = addinfo01Dec.HasValue ? cstQty * addinfo01Dec.Value : cstQty;
            }
            else
            {
                jobordqun = l.Quantity;
            }

            return new JobordItemDto
            {
                Prkey = l.SalesOrderLineId,
                Prddt = l.ProductDate.HasValue ? l.ProductDate.Value.ToString("yyyyMMdd") : null,
                Delvedt = l.PlannedDeliveryDate.HasValue ? l.PlannedDeliveryDate.Value.ToString("yyyyMMdd") : null,
                Shptm = l.Addinfo != null ? l.Addinfo.Addinfo04 : null,
                ShptmName = l.Addinfo != null ? l.Addinfo.Addinfo04Name : null,
                Cuscd = l.SalesOrder != null && l.SalesOrder.Customer != null ? l.SalesOrder.Customer.CustomerCode : null,
                Shpctrcd = l.SalesOrder != null && l.SalesOrder.CustomerDeliveryLocation != null ? l.SalesOrder.CustomerDeliveryLocation.LocationCode : null,
                Shpctrnm = l.SalesOrder != null && l.SalesOrder.CustomerDeliveryLocation != null ? l.SalesOrder.CustomerDeliveryLocation.LocationName : null,
                Itemcd = l.Item != null ? l.Item.ItemCd : null,
                Jobordmernm = l.Item != null ? l.Item.ItemName : null,
                Jobordqun = jobordqun,
                Addinfo01 = l.Addinfo != null ? l.Addinfo.Addinfo01 : null,
                Addinfo05 = l.Addinfo != null ? l.Addinfo.Addinfo05 : null
            };
        }).ToList();
    }

    /// <summary>汁仕分表用：喫食日・品目コードで検索し、喫食日・喫食時間・品目でグループ化して返す。</summary>
    public async Task<List<JuiceSearchGroupDto>> SearchByDeliveryDateGroupedAsync(string delvedt, string? itemcd, string? mealTime = null, CancellationToken ct = default)
    {
        var items = await SearchByDeliveryDateAsync(delvedt, itemcd, mealTime, ct);
        var keySelector = (JobordItemDto x) => (
            Delvedt: x.Delvedt ?? "",
            ShptmDisplay: x.ShptmName ?? x.Shptm ?? "",
            Itemcd: x.Itemcd ?? "",
            Jobordmernm: x.Jobordmernm ?? ""
        );
        var grouped = items
            .GroupBy(keySelector)
            .Select(g => new JuiceSearchGroupDto
            {
                Delvedt = g.Key.Delvedt,
                ShptmDisplay = g.Key.ShptmDisplay,
                Itemcd = g.Key.Itemcd,
                Jobordmernm = g.Key.Jobordmernm,
                Addinfo05 = g.First().Addinfo05,
                Locations = g
                    .GroupBy(x => (x.Shpctrcd ?? "").Trim())
                    .Select(lg =>
                    {
                        var first = lg.First();
                        return new JuiceSearchLocationDto
                        {
                            Shpctrnm = first.Shpctrnm,
                            Jobordqun = lg.Sum(x => x.Jobordqun),
                            Addinfo01 = first.Addinfo01
                        };
                    })
                    .OrderBy(loc => loc.Shpctrnm)
                    .ToList()
            })
            .OrderBy(x => x.Delvedt).ThenBy(x => x.ShptmDisplay).ThenBy(x => x.Itemcd)
            .ToList();
        return grouped;
    }

    /// <summary>納入場所名称マップ。キー=(得意先コード, 納入場所コード)。</summary>
    private async Task<IReadOnlyDictionary<(string Cust, string Loc), string>> LoadLocationNameMapAsync(CancellationToken ct)
    {
        var locs = await _db.CustomerDeliveryLocations.AsNoTracking()
            .Include(l => l.Customer)
            .ToListAsync(ct);
        return locs
            .Where(l => l.Customer != null && !string.IsNullOrWhiteSpace(l.LocationCode))
            .GroupBy(l => (
                GohanSearchFilter.NormalizeCustomer(l.Customer!.CustomerCode),
                (l.LocationCode ?? "").Trim()))
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.LocationName).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n))
                     ?? g.Key.Item2);
    }

    private async Task<IReadOnlyDictionary<string, string>> LoadFoodTypeNameMapAsync(CancellationToken ct)
    {
        if (_otherDb.Database.IsRelational())
        {
            FormattableString sql = $@"
SELECT TRIM(COALESCE(shokushu_code, '')) AS ""Code"", TRIM(COALESCE(shokushu_name, '')) AS ""Name""
FROM m_shokushu";
            var rows = await _otherDb.Database.SqlQuery<ShokushuNameSqlRow>(sql).ToListAsync(ct);
            return rows
                .Where(r => !string.IsNullOrWhiteSpace(r.Code))
                .GroupBy(r => r.Code!.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Name).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)) ?? g.Key,
                    StringComparer.OrdinalIgnoreCase);
        }

        var entities = await _otherDb.Mshokushus.AsNoTracking().ToListAsync(ct);
        return entities
            .Where(s => !string.IsNullOrWhiteSpace(s.ShokushuCode))
            .GroupBy(s => (s.ShokushuCode ?? "").Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.ShokushuName).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)) ?? g.Key,
                StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>弁当箱盛り付け指示書用：喫食日・品目コードで検索。bentoType=okazu|gohan。</summary>
    public async Task<List<BentoSearchGroupDto>> SearchByDeliveryDateForBentoGroupedAsync(
        string delvedt, string? itemcd, string? bentoType = null, CancellationToken ct = default)
    {
        if (BentoSearchFilter.IsGohan(bentoType))
            return await SearchBentoGohanGroupedAsync(delvedt, itemcd, ct);
        return await SearchBentoOkazuGroupedAsync(delvedt, itemcd, ct);
    }

    /// <summary>弁当箱・おかず：cstmeat から得意先240/300/310を検索。</summary>
    private async Task<List<BentoSearchGroupDto>> SearchBentoOkazuGroupedAsync(string delvedt, string? itemcd, CancellationToken ct)
    {
        var delvedtDate = ParseProductDate(delvedt);
        if (!delvedtDate.HasValue)
            throw new ArgumentException("喫食日はYYYYMMDD形式（8桁）で指定してください。", nameof(delvedt));

        var cstmeatRows = await LoadCstmeatDetailRowsAsync(delvedtDate.Value, ct);
        var locationNames = await LoadLocationNameMapAsync(ct);
        var foodTypeNames = await LoadFoodTypeNameMapAsync(ct);
        var itemcdFilter = (itemcd ?? "").Trim();

        var items = new List<JobordItemDto>();
        foreach (var cm in cstmeatRows)
        {
            if (!BentoSearchFilter.IsTargetCustomer(cm.CustCode)) continue;
            if (itemcdFilter.Length > 0 && !(cm.Info17 ?? "").Contains(itemcdFilter, StringComparison.Ordinal)) continue;

            var cust = GohanSearchFilter.NormalizeCustomer(cm.CustCode);
            var loc = (cm.LocCode ?? "").Trim();
            locationNames.TryGetValue((cust, loc), out var locName);
            var foodTypeCode = (cm.FoodType ?? "").Trim();
            foodTypeNames.TryGetValue(foodTypeCode, out var foodTypeName);

            items.Add(new JobordItemDto
            {
                Delvedt = delvedtDate.Value.ToString("yyyyMMdd"),
                Addinfo05 = cm.MealTime,
                Shpctrcd = loc,
                Shpctrnm = locName ?? loc,
                Quantity = cm.Qty,
                CstmeatInfo17 = cm.Info17,
                Jobordmernm = foodTypeName ?? foodTypeCode
            });
        }

        return items
            .GroupBy(x => (Delvedt: x.Delvedt ?? "", Addinfo05: (x.Addinfo05 ?? "").Trim()))
            .Select(g => new BentoSearchGroupDto
            {
                Delvedt = g.Key.Delvedt,
                ShptmDisplay = BaggingEatingTimeLabel.MapFromAddinfo05(g.Key.Addinfo05),
                Addinfo05 = g.Key.Addinfo05,
                Locations = g.Select(x => new BentoSearchLocationDto
                {
                    Shpctrnm = x.Shpctrnm,
                    Quantity = x.Quantity,
                    Info17 = x.CstmeatInfo17,
                    FoodTypeName = x.Jobordmernm,
                    Addinfo05 = x.Addinfo05,
                    Shpctrcd = x.Shpctrcd
                }).ToList()
            })
            .OrderBy(x => x.Delvedt).ThenBy(x => x.Addinfo05)
            .ToList();
    }

    /// <summary>弁当箱・ご飯：得意先240/300/310、addinfo08=1、info14=1、品目3010/3011/3111/3411。</summary>
    private async Task<List<BentoSearchGroupDto>> SearchBentoGohanGroupedAsync(string delvedt, string? itemcd, CancellationToken ct)
    {
        var delvedtDate = ParseProductDate(delvedt);
        if (!delvedtDate.HasValue)
            throw new ArgumentException("喫食日はYYYYMMDD形式（8桁）で指定してください。", nameof(delvedt));

        var query = _db.SalesOrderLines.AsNoTracking()
            .Include(l => l.SalesOrder!)
                .ThenInclude(so => so!.Customer)
            .Include(l => l.SalesOrder!)
                .ThenInclude(so => so!.CustomerDeliveryLocation!)
                .ThenInclude(cdl => cdl!.Addinfo)
            .Include(l => l.Addinfo)
            .Include(l => l.Item!)
            .Where(l => l.PlannedDeliveryDate == delvedtDate)
            .Where(l => l.Item != null
                && l.Item.ItemCd != null
                && (l.Item.ItemCd.StartsWith("3010")
                    || l.Item.ItemCd.StartsWith("3011")
                    || l.Item.ItemCd.StartsWith("3111")
                    || l.Item.ItemCd.StartsWith("3411")));

        if (!string.IsNullOrEmpty(itemcd))
            query = query.Where(l => l.Item != null && l.Item.ItemCd != null && l.Item.ItemCd.Contains(itemcd));

        var lines = await query.OrderBy(l => l.SalesOrderLineId).ToListAsync(ct);

        lines = lines
            .Where(l => BentoSearchFilter.IsTargetCustomer(l.SalesOrder?.Customer?.CustomerCode ?? l.SalesOrder?.CustomerCode))
            .Where(l => BentoSearchFilter.IsTargetGohanAddinfo08(l.SalesOrder?.CustomerDeliveryLocation?.Addinfo?.Addinfo08))
            .Where(l => BentoSearchFilter.IsTargetGohanItemCode(l.Item?.ItemCd))
            .ToList();

        var cstmeatMap = await LoadCstmeatQuantityMapAsync(delvedtDate.Value, ct, info14Filter: "1");

        var items = new List<JobordItemDto>();
        foreach (var l in lines)
        {
            var cuscd = (l.SalesOrder?.Customer?.CustomerCode ?? "").Trim();
            var shpctrcd = (l.SalesOrder?.CustomerDeliveryLocation?.LocationCode ?? "").Trim();
            var mealTime = (l.Addinfo?.Addinfo05 ?? "").Trim();
            var foodType = (l.Addinfo?.Addinfo02 ?? "").Trim();
            if (!cstmeatMap.TryGetValue((cuscd, shpctrcd, mealTime, foodType), out var cstQty))
                continue;

            items.Add(new JobordItemDto
            {
                Delvedt = l.PlannedDeliveryDate.HasValue ? l.PlannedDeliveryDate.Value.ToString("yyyyMMdd") : null,
                Addinfo05 = l.Addinfo?.Addinfo05,
                Shpctrcd = shpctrcd,
                Shpctrnm = l.SalesOrder?.CustomerDeliveryLocation?.LocationName,
                Itemcd = l.Item?.ItemCd,
                Jobordmernm = l.Item?.ItemName,
                Addinfo01 = l.Addinfo?.Addinfo01,
                Quantity = cstQty
            });
        }

        return items
            .GroupBy(x => (
                Delvedt: x.Delvedt ?? "",
                Addinfo05: (x.Addinfo05 ?? "").Trim(),
                Itemcd: x.Itemcd ?? "",
                Jobordmernm: x.Jobordmernm ?? ""))
            .Select(g => new BentoSearchGroupDto
            {
                Delvedt = g.Key.Delvedt,
                ShptmDisplay = BaggingEatingTimeLabel.MapFromAddinfo05(g.Key.Addinfo05),
                Addinfo05 = g.Key.Addinfo05,
                Itemcd = g.Key.Itemcd,
                Jobordmernm = g.Key.Jobordmernm,
                Locations = g.Select(x => new BentoSearchLocationDto
                {
                    Shpctrnm = x.Shpctrnm,
                    Quantity = x.Quantity,
                    Addinfo01 = x.Addinfo01,
                    Addinfo05 = x.Addinfo05,
                    Shpctrcd = x.Shpctrcd
                }).ToList()
            })
            .OrderBy(x => x.Delvedt).ThenBy(x => x.Addinfo05).ThenBy(x => x.Itemcd)
            .ToList();
    }

    /// <summary>弁当箱盛り付け指示書（ご飯）用：喫食日・品目コードで検索。addinfo08Type="0"=BOX, "1"=個別 でフィルター可。</summary>
    public async Task<List<JobordItemDto>> SearchByDeliveryDateForBentoAsync(string delvedt, string? itemcd, string? addinfo08Type = null, CancellationToken ct = default)
    {
        var delvedtDate = ParseProductDate(delvedt);
        if (!delvedtDate.HasValue)
            throw new ArgumentException("喫食日はYYYYMMDD形式（8桁）で指定してください。", nameof(delvedt));
        var query = _db.SalesOrderLines.AsNoTracking()
            .Include(l => l.SalesOrder!)
                .ThenInclude(so => so!.Customer)
            .Include(l => l.SalesOrder!)
                .ThenInclude(so => so!.CustomerDeliveryLocation!)
                .ThenInclude(cdl => cdl!.Addinfo)
            .Include(l => l.Addinfo)
            .Include(l => l.OrderTable)
            .Include(l => l.Item!)
                .ThenInclude(i => i!.AdditionalInformation)
            .Where(l => l.PlannedDeliveryDate == delvedtDate);

        if (!string.IsNullOrEmpty(itemcd))
            query = query.Where(l => l.Item != null && l.Item.ItemCd != null && l.Item.ItemCd.Contains(itemcd));

        var linesBento = await query
            .OrderBy(l => l.SalesOrderLineId)
            .ToListAsync(ct);

        var addinfo08TypeStr = (addinfo08Type ?? "").Trim();
        if (addinfo08TypeStr.Length > 0)
        {
            linesBento = linesBento.Where(l =>
            {
                var s = (l.SalesOrder?.CustomerDeliveryLocation?.Addinfo?.Addinfo08 ?? "").TrimStart();
                return s.StartsWith(addinfo08TypeStr);
            }).ToList();
        }

        // info14='1' のcstmeat行のみを対象とし、マップにない行は弁当箱の出力対象外とする
        var cstmeatMap = await LoadCstmeatQuantityMapAsync(delvedtDate.Value, ct, info14Filter: "1");

        var result = new List<JobordItemDto>();
        foreach (var l in linesBento)
        {
            var cuscd = (l.SalesOrder?.Customer?.CustomerCode ?? "").Trim();
            var shpctrcd = (l.SalesOrder?.CustomerDeliveryLocation?.LocationCode ?? "").Trim();
            var mealTime = (l.Addinfo?.Addinfo05 ?? "").Trim();
            var foodType = (l.Addinfo?.Addinfo02 ?? "").Trim();
            if (!cstmeatMap.TryGetValue((cuscd, shpctrcd, mealTime, foodType), out var cstQty))
                continue;

            result.Add(new JobordItemDto
            {
                Prkey = l.SalesOrderLineId,
                Prddt = l.ProductDate.HasValue ? l.ProductDate.Value.ToString("yyyyMMdd") : null,
                Delvedt = l.PlannedDeliveryDate.HasValue ? l.PlannedDeliveryDate.Value.ToString("yyyyMMdd") : null,
                Shptm = l.Addinfo != null ? l.Addinfo.Addinfo04 : null,
                ShptmName = l.Addinfo != null ? l.Addinfo.Addinfo04Name : null,
                Cuscd = l.SalesOrder != null && l.SalesOrder.Customer != null ? l.SalesOrder.Customer.CustomerCode : null,
                Shpctrcd = l.SalesOrder != null && l.SalesOrder.CustomerDeliveryLocation != null ? l.SalesOrder.CustomerDeliveryLocation.LocationCode : null,
                Shpctrnm = l.SalesOrder != null && l.SalesOrder.CustomerDeliveryLocation != null ? l.SalesOrder.CustomerDeliveryLocation.LocationName : null,
                Itemcd = l.Item != null ? l.Item.ItemCd : null,
                Jobordmernm = l.Item != null ? l.Item.ItemName : null,
                Jobordqun = l.OrderTable != null ? l.OrderTable.Qty : l.Quantity,
                Addinfo01 = l.Addinfo != null ? l.Addinfo.Addinfo01 : null,
                Addinfo01Item = l.Item != null && l.Item.AdditionalInformation != null ? l.Item.AdditionalInformation.Addinfo01 : null,
                Addinfo05 = l.Addinfo != null ? l.Addinfo.Addinfo05 : null,
                Quantity = cstQty,
                Addinfo08 = l.SalesOrder != null && l.SalesOrder.CustomerDeliveryLocation != null
                    ? l.SalesOrder.CustomerDeliveryLocation.Addinfo?.Addinfo08
                    : null
            });
        }
        return result;
    }

    /// <summary>
    /// ご飯盛り付け指示書用：喫食日・品目コードで検索。
    /// 品目コード先頭4桁が 3010/3011/3111/3411 のみ。区分=個人→得意先240/300/310、BOX→得意先200/210。
    /// </summary>
    public async Task<List<JobordItemDto>> SearchByDeliveryDateForGohanAsync(string delvedt, string? itemcd, string? addinfo08Type = null, CancellationToken ct = default)
    {
        var delvedtDate = ParseProductDate(delvedt);
        if (!delvedtDate.HasValue)
            throw new ArgumentException("喫食日はYYYYMMDD形式（8桁）で指定してください。", nameof(delvedt));
        var query = _db.SalesOrderLines.AsNoTracking()
            .Include(l => l.SalesOrder!)
                .ThenInclude(so => so!.Customer)
            .Include(l => l.SalesOrder!)
                .ThenInclude(so => so!.CustomerDeliveryLocation!)
                .ThenInclude(cdl => cdl!.Addinfo)
            .Include(l => l.Addinfo)
            .Include(l => l.OrderTable)
            .Include(l => l.Item!)
                .ThenInclude(i => i!.AdditionalInformation)
            .Where(l => l.PlannedDeliveryDate == delvedtDate)
            .Where(l => l.Item != null
                && l.Item.ItemCd != null
                && (l.Item.ItemCd.StartsWith("3010")
                    || l.Item.ItemCd.StartsWith("3011")
                    || l.Item.ItemCd.StartsWith("3111")
                    || l.Item.ItemCd.StartsWith("3411")));

        if (!string.IsNullOrEmpty(itemcd))
            query = query.Where(l => l.Item != null && l.Item.ItemCd != null && l.Item.ItemCd.Contains(itemcd));

        var linesGohan = await query
            .OrderBy(l => l.SalesOrderLineId)
            .ToListAsync(ct);

        linesGohan = linesGohan
            .Where(l => GohanSearchFilter.IsTargetCustomer(l.SalesOrder?.Customer?.CustomerCode ?? l.SalesOrder?.CustomerCode, addinfo08Type))
            .ToList();

        var addinfo08TypeStr = (addinfo08Type ?? "").Trim();
        if (addinfo08TypeStr.Length > 0)
        {
            linesGohan = linesGohan.Where(l =>
            {
                var s = (l.SalesOrder?.CustomerDeliveryLocation?.Addinfo?.Addinfo08 ?? "").TrimStart();
                return s.StartsWith(addinfo08TypeStr);
            }).ToList();
        }

        var cstmeatMap = await LoadCstmeatQuantityMapAsync(delvedtDate.Value, ct, info14Filter: "1");

        var result = new List<JobordItemDto>();
        foreach (var l in linesGohan)
        {
            if (!GohanSearchFilter.IsTargetItemCode(l.Item?.ItemCd))
                continue;

            var cuscd = (l.SalesOrder?.Customer?.CustomerCode ?? "").Trim();
            var shpctrcd = (l.SalesOrder?.CustomerDeliveryLocation?.LocationCode ?? "").Trim();
            var mealTime = (l.Addinfo?.Addinfo05 ?? "").Trim();
            var foodType = (l.Addinfo?.Addinfo02 ?? "").Trim();
            if (!cstmeatMap.TryGetValue((cuscd, shpctrcd, mealTime, foodType), out var cstQty))
                continue;

            result.Add(new JobordItemDto
            {
                Prkey = l.SalesOrderLineId,
                Prddt = l.ProductDate.HasValue ? l.ProductDate.Value.ToString("yyyyMMdd") : null,
                Delvedt = l.PlannedDeliveryDate.HasValue ? l.PlannedDeliveryDate.Value.ToString("yyyyMMdd") : null,
                Shptm = l.Addinfo != null ? l.Addinfo.Addinfo04 : null,
                ShptmName = l.Addinfo != null ? l.Addinfo.Addinfo04Name : null,
                Cuscd = l.SalesOrder != null && l.SalesOrder.Customer != null ? l.SalesOrder.Customer.CustomerCode : null,
                Shpctrcd = l.SalesOrder != null && l.SalesOrder.CustomerDeliveryLocation != null ? l.SalesOrder.CustomerDeliveryLocation.LocationCode : null,
                Shpctrnm = l.SalesOrder != null && l.SalesOrder.CustomerDeliveryLocation != null ? l.SalesOrder.CustomerDeliveryLocation.LocationName : null,
                Itemcd = l.Item != null ? l.Item.ItemCd : null,
                Jobordmernm = l.Item != null ? l.Item.ItemName : null,
                Jobordqun = l.OrderTable != null ? l.OrderTable.Qty : l.Quantity,
                Addinfo01 = l.Addinfo != null ? l.Addinfo.Addinfo01 : null,
                Addinfo01Item = l.Item != null && l.Item.AdditionalInformation != null ? l.Item.AdditionalInformation.Addinfo01 : null,
                Addinfo05 = l.Addinfo != null ? l.Addinfo.Addinfo05 : null,
                Quantity = cstQty,
                Addinfo08 = l.SalesOrder != null && l.SalesOrder.CustomerDeliveryLocation != null
                    ? l.SalesOrder.CustomerDeliveryLocation.Addinfo?.Addinfo08
                    : null
            });
        }
        return result;
    }

    /// <summary>ご飯盛り付け指示書用：喫食日・品目コードで検索し、喫食日・喫食時間（addinfo05）・品目でグループ化して返す。</summary>
    public async Task<List<BentoSearchGroupDto>> SearchByDeliveryDateForGohanGroupedAsync(string delvedt, string? itemcd, string? addinfo08Type = null, CancellationToken ct = default)
    {
        var items = await SearchByDeliveryDateForGohanAsync(delvedt, itemcd, addinfo08Type, ct);
        var keySelector = (JobordItemDto x) => (
            Delvedt: x.Delvedt ?? "",
            Addinfo05: (x.Addinfo05 ?? "").Trim(),
            Itemcd: x.Itemcd ?? "",
            Jobordmernm: x.Jobordmernm ?? ""
        );
        var grouped = items
            .GroupBy(keySelector)
            .Select(g => new BentoSearchGroupDto
            {
                Delvedt = g.Key.Delvedt,
                ShptmDisplay = BaggingEatingTimeLabel.MapFromAddinfo05(g.Key.Addinfo05),
                Addinfo05 = g.Key.Addinfo05,
                Itemcd = g.Key.Itemcd,
                Jobordmernm = g.Key.Jobordmernm,
                Locations = g.Select(x => new BentoSearchLocationDto
                {
                    Shpctrnm = x.Shpctrnm,
                    Jobordqun = x.Jobordqun,
                    Quantity = x.Quantity,
                    Addinfo01 = x.Addinfo01,
                    Addinfo08 = x.Addinfo08,
                    Cuscd = x.Cuscd,
                    Shpctrcd = x.Shpctrcd
                }).ToList()
            })
            .OrderBy(x => x.Delvedt).ThenBy(x => x.Addinfo05).ThenBy(x => x.Itemcd)
            .ToList();
        return grouped;
    }

    /// <summary>salesorderlineid で受注明細を取得（全リレーション付き）。Bom は parentitemcode で別取得。</summary>
    public async Task<List<BaggingDetailRow>> SearchDetailByPrkeysAsync(IReadOnlyList<long> prkeys, CancellationToken ct = default)
    {
        if (prkeys == null || prkeys.Count == 0)
            return new List<BaggingDetailRow>();

        var idSet = prkeys.ToHashSet();
        var lines = await _db.SalesOrderLines
            .AsNoTracking()
            .Where(l => idSet.Contains(l.SalesOrderLineId))
            .Include(l => l.SalesOrder!)
                .ThenInclude(so => so!.Customer)
            .Include(l => l.SalesOrder!)
                .ThenInclude(so => so!.CustomerDeliveryLocation)
            .Include(l => l.Addinfo)
            .Include(l => l.Item!)
                .ThenInclude(i => i!.AdditionalInformation)
            .Include(l => l.Item!)
                .ThenInclude(i => i!.Unit0)
            .Include(l => l.Item!)
                .ThenInclude(i => i!.WorkCenterMappings!)
                .ThenInclude(m => m.Workcenter)
            .Include(l => l.Item!)
                .ThenInclude(i => i!.Classification1)
            .Include(l => l.CustomerItem!)
                .ThenInclude(ci => ci!.Customer)
            .OrderBy(l => l.SalesOrderLineId)
            .ToListAsync(ct);

        if (lines.Count == 0)
            return new List<BaggingDetailRow>();

        var itemCds = lines.Where(l => l.Item != null && !string.IsNullOrEmpty(l.Item.ItemCd)).Select(l => l.Item!.ItemCd!).Distinct().ToList();
        var bomsByParent = new Dictionary<string, List<(Bom b, Item? child, Unit? unit)>>();
        foreach (var itemCd in itemCds)
        {
            var boms = await _db.Boms.AsNoTracking()
                .Where(b => b.ParentItemCd == itemCd)
                .OrderBy(b => b.ProductionOrder ?? decimal.MaxValue)
                .ThenBy(b => b.ChildItemCd)
                .Include(b => b.ChildItem)
                    .ThenInclude(c => c!.Unit0)
                .Include(b => b.ChildItem)
                    .ThenInclude(c => c!.AdditionalInformation)
                .ToListAsync(ct);
            var list = boms.Select(b => (b, b.ChildItem, b.ChildItem?.Unit0)).ToList();
            bomsByParent[itemCd] = list;
        }

        var rows = new List<BaggingDetailRow>();
        foreach (var l in lines)
        {
            var item = l.Item;
            var addInfo = item?.AdditionalInformation;
            var so = l.SalesOrder;
            var cust = so?.Customer;
            var loc = so?.CustomerDeliveryLocation;
            var divisor = BaggingDivisorResolver.ResolveFromAddInfo(addInfo);
            var car0 = addInfo?.Car0 ?? BaggingDivisorResolver.ParseStdToDecimal(addInfo?.Std) ?? 1m;
            var defaultSpecQty = BaggingDivisorResolver.ParseStdToDecimal(addInfo?.Std);

            var seasoningBoms = new List<SeasoningBomRow>();
            var bomListResolved = new List<(Bom b, Item? child, Unit? unit)>();
            if (item != null && bomsByParent.TryGetValue(item.ItemCd ?? "", out var bomList))
            {
                bomListResolved = bomList;
                foreach (var (b, child, unit) in bomList)
                {
                    seasoningBoms.Add(new SeasoningBomRow
                    {
                        Otp = b.OutputQty,
                        Amu = b.InputQty,
                        ChildItemCd = b.ChildItemCd,
                        ChildUnitName = unit?.UnitName
                    });
                }
            }

            var routs = (item?.WorkCenterMappings ?? new List<ItemWorkCenterMapping>())
                .Select(m => (m, m.Workcenter))
                .ToList();

            rows.Add(new BaggingDetailRow
            {
                Prkey = l.SalesOrderLineId,
                Prddt = l.ProductDate?.ToString("yyyyMMdd"),
                Delvedt = l.PlannedDeliveryDate?.ToString("yyyyMMdd"),
                Shptm = l.Addinfo?.Addinfo04,
                ShptmName = l.Addinfo?.Addinfo05Name,
                Addinfo05 = l.Addinfo?.Addinfo05,
                Cuscd = cust?.CustomerCode,
                Shpctrcd = loc?.LocationCode,
                Itemcd = item?.ItemCd,
                Jobordqun = l.Quantity,
                Jobordmernm = item?.ItemName,
                Jobordno = so?.SalesOrderNo,
                ItemId = item?.ItemId,
                Shpctrnm = loc?.LocationName ?? loc?.LocationCode ?? "未指定",
                Divisor = divisor,
                Car0 = car0,
                DefaultSpecQty = defaultSpecQty,
                SeasoningBoms = seasoningBoms,
                Item = EntityToDtoMapper.ToItemDetailDto(item, addInfo, item?.Unit0, routs),
                Shpctr = EntityToDtoMapper.ToShpctrDetailDto(loc, cust),
                Cusmcd = EntityToDtoMapper.ToCusmcdDetailDto(l.CustomerItem, l.CustomerItem?.Customer),
                Mboms = bomListResolved.Select(t => EntityToDtoMapper.ToMbomDetailDto(t.b, t.child, t.unit)).ToList()
            });
        }

        return rows;
    }

    /// <summary>
    /// 現品票：ordertable を納期で明細検索。
    /// 対象はMO品目かつBOM childitemcodeに存在しない品目（親品目が存在しない品目）のみ。
    /// 大分類・品目コード・作業区・倉庫で絞り込み可能（すべて任意）。
    /// </summary>
    public async Task<List<ProductLabelRowDto>> SearchProductLabelAsync(
        string needdateYyyymmdd,
        long? majorClassificationId,
        string? itemCode = null,
        long? workcenterId = null,
        long? warehouseId = null,
        CancellationToken ct = default)
    {
        var date = ParseProductDate(needdateYyyymmdd);
        if (!date.HasValue)
            throw new ArgumentException("納期はYYYYMMDD形式（8桁）で指定してください。", nameof(needdateYyyymmdd));

        string? majorCode = null;
        if (majorClassificationId.HasValue && majorClassificationId.Value > 0)
        {
            majorCode = await _db.MajorClassifications.AsNoTracking()
                .Where(m => m.MajorClassificationId == majorClassificationId.Value)
                .Select(m => m.MajorClassificationCode)
                .FirstOrDefaultAsync(ct);
            if (string.IsNullOrEmpty(majorCode))
                return new List<ProductLabelRowDto>();
        }

        var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
        var shouldClose = conn.State != ConnectionState.Open;
        if (shouldClose) await conn.OpenAsync(ct);
        try
        {
            var joinWh = warehouseId.HasValue && warehouseId.Value > 0;
            var sql = new StringBuilder("""
                SELECT
                  ot.ordertableid,
                  ot.releasedate,
                  COALESCE(ot.itemcode, ''),
                  COALESCE(i.itemname, ''),
                  COALESCE(ot.qty, 0),
                  COALESCE(w.workcentername, ''),
                  (SELECT COUNT(*) FROM bom b WHERE b.parentitemcode = ot.itemcode)
                FROM ordertable ot
                INNER JOIN item i ON i.itemcode = ot.itemcode
                LEFT JOIN workcenter w ON (
                  w.workcentercode = TRIM(BOTH FROM ot.workcentercode)
                  OR w.workcenterid::text = TRIM(BOTH FROM ot.workcentercode)
                )
                """);
            if (joinWh)
                sql.AppendLine("LEFT JOIN warehouses wh ON wh.warehousecode = TRIM(COALESCE(i.warehousecode, ''))");

            sql.AppendLine("WHERE ot.needdate = @needDate");
            sql.AppendLine("AND TRIM(COALESCE(ot.ordertype, '')) = 'MO'");
            sql.AppendLine("AND NOT EXISTS (SELECT 1 FROM bom b2 WHERE b2.childitemcode = ot.itemcode)");

            await using var cmd = new NpgsqlCommand("", conn);
            cmd.Parameters.AddWithValue("needDate", date.Value);

            if (majorCode != null)
            {
                sql.AppendLine("AND TRIM(COALESCE(i.majorclassificationcode, '')) = TRIM(@majorCode)");
                cmd.Parameters.AddWithValue("majorCode", majorCode.Trim());
            }
            if (!string.IsNullOrWhiteSpace(itemCode))
            {
                sql.AppendLine("AND TRIM(COALESCE(ot.itemcode, '')) ILIKE @itemCodePattern");
                cmd.Parameters.AddWithValue("itemCodePattern", $"%{itemCode.Trim()}%");
            }
            if (workcenterId.HasValue && workcenterId.Value > 0)
            {
                sql.AppendLine("AND w.workcenterid = @workcenterId");
                cmd.Parameters.AddWithValue("workcenterId", workcenterId.Value);
            }
            if (joinWh)
            {
                sql.AppendLine("AND wh.warehouseid = @warehouseId");
                cmd.Parameters.AddWithValue("warehouseId", warehouseId!.Value);
            }
            sql.AppendLine("ORDER BY ot.ordertableid");
            cmd.CommandText = sql.ToString();

            var rawList = new List<(long Id, string ReleaseDate, string ItemCode, string ItemName, decimal Qty, string WorkcenterName, int ChildCount)>();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                rawList.Add((
                    reader.GetInt64(0),
                    ReadDateOnlyNullable(reader, 1)?.ToString("yyyyMMdd") ?? "",
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetDecimal(4),
                    reader.GetString(5),
                    (int)reader.GetInt64(6)));
            }

            // 同一品目コードを合算（数量合計・ordertableid 集約）
            var posMap = new Dictionary<string, int>();
            for (var i = 0; i < rawList.Count; i++)
                if (!posMap.ContainsKey(rawList[i].ItemCode))
                    posMap[rawList[i].ItemCode] = i;

            var list = rawList
                .GroupBy(r => r.ItemCode)
                .OrderBy(g => posMap[g.Key])
                .Select(g =>
                {
                    var ordered = g.OrderBy(r => r.Id).ToList();
                    var first = ordered[0];
                    return new ProductLabelRowDto
                    {
                        OrderTableIds = ordered.Select(r => r.Id).ToList(),
                        ReleaseDate = first.ReleaseDate,
                        ItemCode = first.ItemCode,
                        ItemName = first.ItemName,
                        Qty = g.Sum(r => r.Qty),
                        WorkcenterName = first.WorkcenterName,
                        ChildCount = first.ChildCount,
                    };
                })
                .ToList();

            return list;
        }
        finally
        {
            if (shouldClose && conn.State == ConnectionState.Open)
                await conn.CloseAsync();
        }
    }

    /// <summary>
    /// 現品票 PDF 用：ordertableid 一覧を BOM（1階層）展開して取得。
    /// 1 ordertable に複数の子品目がある場合は子品目ごとに 1 行。子品目なしの場合は子品目フィールドが空の 1 行。
    /// 順序: ordertableid → bom.productionorder → bom.childitemcode。
    /// </summary>
    public async Task<List<ProductLabelOrderSqlRow>> LoadProductLabelOrdersByIdsAsync(IReadOnlyList<long> orderTableIds, CancellationToken ct = default)
    {
        if (orderTableIds == null || orderTableIds.Count == 0)
            return new List<ProductLabelOrderSqlRow>();

        var ids = orderTableIds.Where(id => id > 0).Distinct().ToArray();
        if (ids.Length == 0)
            return new List<ProductLabelOrderSqlRow>();

        var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
        var shouldClose = conn.State != ConnectionState.Open;
        if (shouldClose)
            await conn.OpenAsync(ct);
        try
        {
            await using var cmd = new NpgsqlCommand(
                """
                SELECT
                  ot.ordertableid,
                  ot.releasedate,
                  COALESCE(ot.itemcode, ''),
                  COALESCE(i.itemname, ''),
                  COALESCE(ot.qty, 0),
                  COALESCE(w.workcentername, ''),
                  COALESCE(b.childitemcode, ''),
                  COALESCE(ci.itemname, ''),
                  CASE
                    WHEN b.childitemcode IS NOT NULL AND COALESCE(b.outputqty, 0) <> 0
                      THEN COALESCE(ot.qty, 0) * COALESCE(b.inputqty, 0) / b.outputqty
                    WHEN b.childitemcode IS NOT NULL
                      THEN COALESCE(b.inputqty, 0)
                    ELSE COALESCE(ot.qty, 0)
                  END,
                  COALESCE(NULLIF(TRIM(COALESCE(cu.unitname, cu.unitsymbol, '')), ''), ''),
                  COALESCE(i.shelflifedays, 0)
                FROM ordertable ot
                INNER JOIN item i ON i.itemcode = ot.itemcode
                LEFT JOIN workcenter w ON (
                  w.workcentercode = TRIM(BOTH FROM ot.workcentercode)
                  OR w.workcenterid::text = TRIM(BOTH FROM ot.workcentercode)
                )
                LEFT JOIN bom b ON b.parentitemcode = ot.itemcode
                LEFT JOIN item ci ON ci.itemcode = b.childitemcode
                LEFT JOIN unit cu ON cu.unitcode = ci.unitcode0
                WHERE ot.ordertableid = ANY(@ids)
                ORDER BY ot.ordertableid, b.productionorder NULLS LAST, b.childitemcode
                """, conn);
            cmd.Parameters.Add(new NpgsqlParameter("ids", NpgsqlDbType.Bigint | NpgsqlDbType.Array) { Value = ids });

            var list = new List<ProductLabelOrderSqlRow>();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                list.Add(new ProductLabelOrderSqlRow
                {
                    OrderTableId = reader.GetInt64(0),
                    ReleaseDate = ReadDateOnlyNullable(reader, 1),
                    ParentItemCode = reader.GetString(2),
                    ParentItemName = reader.GetString(3),
                    Qty = reader.GetDecimal(4),
                    WorkcenterName = reader.GetString(5),
                    ChildItemCode = reader.GetString(6),
                    ChildItemName = reader.GetString(7),
                    ChildQty = reader.GetDecimal(8),
                    ChildUnitName = reader.GetString(9),
                    ShelflifeDays = reader.GetInt32(10),
                });
            }

            return AggregateProductLabelRows(list);
        }
        finally
        {
            if (shouldClose && conn.State == ConnectionState.Open)
                await conn.CloseAsync();
        }
    }

    /// <summary>
    /// 現品票 PDF 用：選択した ordertable から BOM を再帰探索し、指示書種別条件に一致する子品目行を返す。
    /// instructionType: "cut"=50/51, "seasoning"=55, "cooking"=50以外。空の場合は全子品目。
    /// 数量は各BOM階層の inputqty/outputqty を乗算して累積する（最大10階層）。
    /// </summary>
    public async Task<List<ProductLabelOrderSqlRow>> LoadProductLabelOrdersByBomTraversalAsync(
        IReadOnlyList<long> orderTableIds,
        string? instructionType,
        CancellationToken ct = default)
    {
        if (orderTableIds == null || orderTableIds.Count == 0)
            return new List<ProductLabelOrderSqlRow>();

        var ids = orderTableIds.Where(id => id > 0).Distinct().ToArray();
        if (ids.Length == 0)
            return new List<ProductLabelOrderSqlRow>();

        var typeFilter = instructionType?.Trim().ToLowerInvariant() switch
        {
            "cut"      => "LEFT(TRIM(COALESCE(bt.current_itemcode, '')), 2) IN ('50', '51')",
            "seasoning" => "LEFT(TRIM(COALESCE(bt.current_itemcode, '')), 2) = '55'",
            "cooking"  => "LEFT(TRIM(COALESCE(bt.current_itemcode, '')), 2) <> '50'",
            _          => "TRUE",
        };

        var sql = $"""
            WITH RECURSIVE bom_tree AS (
              SELECT
                ot.ordertableid,
                ot.releasedate,
                ot.itemcode        AS root_itemcode,
                ot.qty             AS accumulated_qty,
                ot.itemcode        AS current_itemcode,
                0                  AS depth
              FROM ordertable ot
              WHERE ot.ordertableid = ANY(@ids)
              UNION ALL
              SELECT
                bt.ordertableid,
                bt.releasedate,
                bt.root_itemcode,
                CASE WHEN COALESCE(b.outputqty, 0::numeric) <> 0
                  THEN bt.accumulated_qty * COALESCE(b.inputqty, 0::numeric) / b.outputqty
                  ELSE COALESCE(b.inputqty, 0::numeric)
                END,
                b.childitemcode,
                bt.depth + 1
              FROM bom_tree bt
              INNER JOIN bom b ON b.parentitemcode = bt.current_itemcode
                AND b.childitemcode IS NOT NULL
              WHERE bt.depth < 10
            )
            SELECT DISTINCT ON (bt.ordertableid, bt.current_itemcode)
              bt.ordertableid,
              bt.releasedate,
              bt.root_itemcode,
              COALESCE(ri.itemname, ''),
              COALESCE(ot.qty, 0),
              COALESCE(w.workcentername, ''),
              bt.current_itemcode,
              COALESCE(ci.itemname, ''),
              bt.accumulated_qty,
              COALESCE(NULLIF(TRIM(COALESCE(cu.unitname, cu.unitsymbol, '')), ''), ''),
              COALESCE(ci.shelflifedays, 0)
            FROM bom_tree bt
            INNER JOIN ordertable ot ON ot.ordertableid = bt.ordertableid
            INNER JOIN item ri ON ri.itemcode = bt.root_itemcode
            LEFT JOIN workcenter w ON (
              w.workcentercode = TRIM(BOTH FROM ot.workcentercode)
              OR w.workcenterid::text = TRIM(BOTH FROM ot.workcentercode)
            )
            INNER JOIN item ci ON ci.itemcode = bt.current_itemcode
            LEFT JOIN unit cu ON cu.unitcode = ci.unitcode0
            WHERE bt.depth > 0
              AND {typeFilter}
            ORDER BY bt.ordertableid, bt.current_itemcode, bt.depth
            """;

        var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
        var shouldClose = conn.State != ConnectionState.Open;
        if (shouldClose) await conn.OpenAsync(ct);
        try
        {
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.Add(new NpgsqlParameter("ids", NpgsqlDbType.Bigint | NpgsqlDbType.Array) { Value = ids });

            var list = new List<ProductLabelOrderSqlRow>();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                list.Add(new ProductLabelOrderSqlRow
                {
                    OrderTableId  = reader.GetInt64(0),
                    ReleaseDate   = ReadDateOnlyNullable(reader, 1),
                    ParentItemCode = reader.GetString(2),
                    ParentItemName = reader.GetString(3),
                    Qty            = reader.GetDecimal(4),
                    WorkcenterName = reader.GetString(5),
                    ChildItemCode  = reader.GetString(6),
                    ChildItemName  = reader.GetString(7),
                    ChildQty       = reader.GetDecimal(8),
                    ChildUnitName  = reader.GetString(9),
                    ShelflifeDays  = reader.GetInt32(10),
                });
            }
            return AggregateProductLabelRows(list);
        }
        finally
        {
            if (shouldClose && conn.State == ConnectionState.Open)
                await conn.CloseAsync();
        }
    }

    /// <summary>
    /// 同一 (ParentItemCode, ChildItemCode) の行を合算する。
    /// ChildQty・Qty を合計し、他フィールドは先頭行の値を使用する。
    /// </summary>
    private static List<ProductLabelOrderSqlRow> AggregateProductLabelRows(List<ProductLabelOrderSqlRow> rows)
    {
        if (rows.Count == 0) return rows;

        var positionMap = new Dictionary<(string, string), int>();
        for (var i = 0; i < rows.Count; i++)
        {
            var key = (rows[i].ParentItemCode, rows[i].ChildItemCode);
            if (!positionMap.ContainsKey(key))
                positionMap[key] = i;
        }

        return rows
            .GroupBy(r => (r.ParentItemCode, r.ChildItemCode))
            .OrderBy(g => positionMap[g.Key])
            .Select(g =>
            {
                var first = g.First();
                return new ProductLabelOrderSqlRow
                {
                    OrderTableId   = first.OrderTableId,
                    ReleaseDate    = first.ReleaseDate,
                    ParentItemCode = first.ParentItemCode,
                    ParentItemName = first.ParentItemName,
                    Qty            = g.Sum(r => r.Qty),
                    WorkcenterName = first.WorkcenterName,
                    ChildItemCode  = first.ChildItemCode,
                    ChildItemName  = first.ChildItemName,
                    ChildQty       = g.Sum(r => r.ChildQty),
                    ChildUnitName  = first.ChildUnitName,
                    ShelflifeDays  = first.ShelflifeDays,
                };
            })
            .ToList();
    }

    private static DateOnly? ReadDateOnlyNullable(NpgsqlDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
            return null;
        var o = reader.GetValue(ordinal);
        if (o is DateOnly d)
            return d;
        if (o is DateTime dt)
            return DateOnly.FromDateTime(dt);
        return null;
    }

    public async Task<List<MajorClassificationOptionDto>> ListMajorClassificationsAsync(CancellationToken ct = default)
    {
        return await _db.MajorClassifications.AsNoTracking()
            .OrderBy(m => m.MajorClassificationCode ?? "")
            .Select(m => new MajorClassificationOptionDto
            {
                Id = m.MajorClassificationId,
                Code = m.MajorClassificationCode ?? "",
                Name = m.MajorClassificationName ?? ""
            })
            .ToListAsync(ct);
    }

    /// <summary>
    /// 受注明細 ID（salesorderlineid）に紐づく ordertable.ordertableid を、引数の並びで返す（現品票 PDF 用）。
    /// ordertable が無い・ordertableid が無い明細はスキップする。
    /// </summary>
    public async Task<List<long>> GetOrderTableIdsBySalesOrderLineIdsAsync(
        IReadOnlyList<long> salesOrderLineIds,
        CancellationToken ct = default)
    {
        if (salesOrderLineIds == null || salesOrderLineIds.Count == 0)
            return new List<long>();

        var orderedLineIds = salesOrderLineIds.Where(id => id > 0).ToList();
        if (orderedLineIds.Count == 0)
            return new List<long>();

        var idSet = orderedLineIds.ToHashSet();
        var rows = await _db.OrderTables.AsNoTracking()
            .Where(o => idSet.Contains(o.SalesOrderLineId)
                        && o.OrderTableId != null
                        && o.OrderTableId.Value > 0)
            .Select(o => new { o.SalesOrderLineId, OrderTableId = o.OrderTableId!.Value })
            .ToListAsync(ct);

        var byLine = rows.ToDictionary(r => r.SalesOrderLineId, r => r.OrderTableId);
        var result = new List<long>(orderedLineIds.Count);
        foreach (var lineId in orderedLineIds)
        {
            if (byLine.TryGetValue(lineId, out var otId))
                result.Add(otId);
        }

        return result;
    }

    /// <summary>
    /// craftlineaxother.cstmeat から喫食日の食数マップを取得する。
    /// キー: (得意先コード, 納入場所コード, 喫食時間, 食種コード) → 食数。
    /// </summary>
    /// <param name="info14Filter">null = 全件、"1" = info14='1' のみ（弁当箱ご飯用）</param>
    private async Task<List<CstmeatDetailRow>> LoadCstmeatDetailRowsAsync(
        DateOnly date, CancellationToken ct, string? info14Filter = null)
    {
        var dateStr = date.ToString("yyyyMMdd");

        if (_otherDb.Database.IsRelational())
        {
            FormattableString sql;
            if (info14Filter != null)
                sql = $@"
SELECT
  TRIM(COALESCE(info01, '')) AS ""CustCode"",
  TRIM(COALESCE(info02, '')) AS ""LocCode"",
  TRIM(COALESCE(info04, '')) AS ""MealTime"",
  TRIM(COALESCE(info05, '')) AS ""FoodType"",
  TRIM(COALESCE(info17, '')) AS ""Info17"",
  COALESCE(CAST(NULLIF(TRIM(COALESCE(info07, '')), '') AS DECIMAL), 0) AS ""Qty""
FROM cstmeat
WHERE info03 = {dateStr}
  AND TRIM(COALESCE(info14, '')) = {info14Filter}
";
            else
                sql = $@"
SELECT
  TRIM(COALESCE(info01, '')) AS ""CustCode"",
  TRIM(COALESCE(info02, '')) AS ""LocCode"",
  TRIM(COALESCE(info04, '')) AS ""MealTime"",
  TRIM(COALESCE(info05, '')) AS ""FoodType"",
  TRIM(COALESCE(info17, '')) AS ""Info17"",
  COALESCE(CAST(NULLIF(TRIM(COALESCE(info07, '')), '') AS DECIMAL), 0) AS ""Qty""
FROM cstmeat
WHERE info03 = {dateStr}
";
            var rows = await _otherDb.Database
                .SqlQuery<CstmeatDetailSqlRow>(sql)
                .ToListAsync(ct);
            return rows.Select(r => new CstmeatDetailRow
            {
                CustCode = r.CustCode,
                LocCode = r.LocCode,
                MealTime = r.MealTime,
                FoodType = r.FoodType,
                Info17 = r.Info17,
                Qty = r.Qty
            }).ToList();
        }

        IQueryable<Cstmeat> q = _otherDb.Cstmeats.AsNoTracking()
            .Where(c => c.Info03 == dateStr);
        if (info14Filter != null)
            q = q.Where(c => (c.Info14 ?? "").Trim() == info14Filter);
        var entities = await q.ToListAsync(ct);
        return entities.Select(c => new CstmeatDetailRow
        {
            CustCode = c.Info01,
            LocCode = c.Info02,
            MealTime = c.Info04,
            FoodType = c.Info05,
            Info17 = c.Info17,
            Qty = decimal.TryParse((c.Info07 ?? "").Trim(), out var qv) ? qv : 0
        }).ToList();
    }

    /// <summary>
    /// craftlineaxother.cstmeat から喫食日の食数マップを取得する。
    /// キー: (得意先コード, 納入場所コード, 喫食時間, 食種コード) → 食数。
    /// </summary>
    /// <param name="info14Filter">null = 全件、"1" = info14='1' のみ（弁当箱ご飯用）</param>
    private async Task<IReadOnlyDictionary<(string CustCode, string LocCode, string MealTime, string FoodType), decimal>>
        LoadCstmeatQuantityMapAsync(DateOnly date, CancellationToken ct, string? info14Filter = null)
    {
        var dateStr = date.ToString("yyyyMMdd");

        if (_otherDb.Database.IsRelational())
        {
            FormattableString sql;
            if (info14Filter != null)
                sql = $@"
SELECT
  TRIM(COALESCE(info01, '')) AS ""CustCode"",
  TRIM(COALESCE(info02, '')) AS ""LocCode"",
  TRIM(COALESCE(info04, '')) AS ""MealTime"",
  TRIM(COALESCE(info05, '')) AS ""FoodType"",
  COALESCE(CAST(NULLIF(TRIM(COALESCE(info07, '')), '') AS DECIMAL), 0) AS ""Qty""
FROM cstmeat
WHERE info03 = {dateStr}
  AND TRIM(COALESCE(info14, '')) = {info14Filter}
";
            else
                sql = $@"
SELECT
  TRIM(COALESCE(info01, '')) AS ""CustCode"",
  TRIM(COALESCE(info02, '')) AS ""LocCode"",
  TRIM(COALESCE(info04, '')) AS ""MealTime"",
  TRIM(COALESCE(info05, '')) AS ""FoodType"",
  COALESCE(CAST(NULLIF(TRIM(COALESCE(info07, '')), '') AS DECIMAL), 0) AS ""Qty""
FROM cstmeat
WHERE info03 = {dateStr}
";
            var rows = await _otherDb.Database
                .SqlQuery<CstmeatQuantitySqlRow>(sql)
                .ToListAsync(ct);

            return rows
                .GroupBy(r => (StripLeadingZeros(r.CustCode), r.LocCode ?? "", r.MealTime ?? "", r.FoodType ?? ""))
                .ToDictionary(g => g.Key, g => g.Sum(r => r.Qty));
        }
        else
        {
            IQueryable<Cstmeat> q = _otherDb.Cstmeats.AsNoTracking()
                .Where(c => c.Info03 == dateStr);
            if (info14Filter != null)
                q = q.Where(c => (c.Info14 ?? "").Trim() == info14Filter);
            var entities = await q.ToListAsync(ct);

            return entities
                .GroupBy(c => (
                    StripLeadingZeros((c.Info01 ?? "").Trim()),
                    (c.Info02 ?? "").Trim(),
                    (c.Info04 ?? "").Trim(),
                    (c.Info05 ?? "").Trim()))
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(c => decimal.TryParse((c.Info07 ?? "").Trim(), out var q) ? q : 0));
        }
    }

    /// <summary>先頭ゼロを除去して正規化する。"000210" → "210"、"0" → "0"。</summary>
    private static string StripLeadingZeros(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var t = s.TrimStart('0');
        return t.Length == 0 ? "0" : t;
    }

    private static decimal? TryParseAddinfoDivisor(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (decimal.TryParse(text.Trim(), System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var v) && v > 0)
            return v;
        return null;
    }

    private static DateOnly? ParseProductDate(string? prddt)
    {
        if (string.IsNullOrEmpty(prddt) || prddt.Length != 8) return null;
        if (int.TryParse(prddt.AsSpan(0, 4), out var y) && int.TryParse(prddt.AsSpan(4, 2), out var m) && int.TryParse(prddt.AsSpan(6, 2), out var d))
            return new DateOnly(y, m, d);
        return null;
    }

}

internal sealed class CstmeatQuantitySqlRow
{
    public string? CustCode { get; set; }
    public string? LocCode { get; set; }
    public string? MealTime { get; set; }
    public string? FoodType { get; set; }
    public decimal Qty { get; set; }
}

internal sealed class CstmeatDetailSqlRow
{
    public string? CustCode { get; set; }
    public string? LocCode { get; set; }
    public string? MealTime { get; set; }
    public string? FoodType { get; set; }
    public string? Info17 { get; set; }
    public decimal Qty { get; set; }
}

internal sealed class CstmeatDetailRow
{
    public string? CustCode { get; set; }
    public string? LocCode { get; set; }
    public string? MealTime { get; set; }
    public string? FoodType { get; set; }
    public string? Info17 { get; set; }
    public decimal Qty { get; set; }
}

internal sealed class ShokushuNameSqlRow
{
    public string? Code { get; set; }
    public string? Name { get; set; }
}
