using Microsoft.EntityFrameworkCore;
using BaggingInstructions.Api.Core;
using BaggingInstructions.Api.Entities;
using BaggingInstructions.Api.DTOs;
using BaggingInstructions.Api.QueryResults;

namespace BaggingInstructions.Api.Services;

public class SearchService
{
    private readonly AppDbContext _db;

    public SearchService(AppDbContext db)
    {
        _db = db;
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
            Shptm = l.Addinfo != null ? l.Addinfo.Addinfo01 : null,
            Cuscd = l.SalesOrder != null && l.SalesOrder.Customer != null ? l.SalesOrder.Customer.CustomerCode : null,
            Shpctrcd = l.SalesOrder != null && l.SalesOrder.CustomerDeliveryLocation != null ? l.SalesOrder.CustomerDeliveryLocation.LocationCode : null,
            Itemcd = l.Item != null ? l.Item.ItemCd : null,
            Jobordmernm = l.Item != null ? l.Item.ItemName : null,
            Jobordqun = l.Quantity
        }).ToList();
    }

    /// <summary>袋詰用：製造日・品目コードで受注明細を検索し、製造日×品目で合算したグループを返す。</summary>
    public async Task<List<BaggingSearchGroupDto>> SearchBaggingGroupedAsync(string prddt, string? itemcd, CancellationToken ct = default)
    {
        var prddtDate = ParseProductDate(prddt);
        if (!prddtDate.HasValue)
            throw new ArgumentException("製造日はYYYYMMDD形式（8桁）で指定してください。", nameof(prddt));

        var query = _db.SalesOrderLines.AsNoTracking()
            .Include(l => l.Item!)
                .ThenInclude(i => i!.Unit0)
            .Where(l => l.ProductDate == prddtDate);

        if (!string.IsNullOrEmpty(itemcd))
            query = query.Where(l => l.Item != null && l.Item.ItemCd != null && l.Item.ItemCd.Contains(itemcd));

        var lines = await query
            .OrderBy(l => l.SalesOrderLineId)
            .ToListAsync(ct);

        return lines
            .Where(l => l.Item != null && !string.IsNullOrEmpty(l.Item.ItemCd))
            .GroupBy(l => l.Item!.ItemCd!)
            .Select(g =>
            {
                var first = g.First();
                var item = first.Item!;
                return new BaggingSearchGroupDto
                {
                    Prddt = prddtDate.Value.ToString("yyyyMMdd"),
                    Itemcd = g.Key,
                    Itemnm = item.ItemName,
                    TotalJobordqun = g.Sum(x => x.Quantity),
                    UnitCode = item.Unit0?.UnitCode,
                    UnitName = item.Unit0?.UnitName,
                    LinePrkeys = g.Select(x => x.SalesOrderLineId).OrderBy(id => id).ToList()
                };
            })
            .OrderBy(x => x.Itemcd, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>汁仕分表用：喫食日・品目コードで検索。delvedt は YYYYMMDD、itemcd は部分一致。</summary>
    public async Task<List<JobordItemDto>> SearchByDeliveryDateAsync(string delvedt, string? itemcd, CancellationToken ct = default)
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
            .Where(l => l.PlannedDeliveryDate == delvedtDate);

        if (!string.IsNullOrEmpty(itemcd))
            query = query.Where(l => l.Item != null && l.Item.ItemCd != null && l.Item.ItemCd.Contains(itemcd));

        var linesJuice = await query
            .OrderBy(l => l.SalesOrderLineId)
            .ToListAsync(ct);

        return linesJuice.Select(l => new JobordItemDto
        {
            Prkey = l.SalesOrderLineId,
            Prddt = l.ProductDate.HasValue ? l.ProductDate.Value.ToString("yyyyMMdd") : null,
            Delvedt = l.PlannedDeliveryDate.HasValue ? l.PlannedDeliveryDate.Value.ToString("yyyyMMdd") : null,
            Shptm = l.Addinfo != null ? l.Addinfo.Addinfo01 : null,
            ShptmName = l.Addinfo != null ? l.Addinfo.Addinfo01Name : null,
            Cuscd = l.SalesOrder != null && l.SalesOrder.Customer != null ? l.SalesOrder.Customer.CustomerCode : null,
            Shpctrcd = l.SalesOrder != null && l.SalesOrder.CustomerDeliveryLocation != null ? l.SalesOrder.CustomerDeliveryLocation.LocationCode : null,
            Shpctrnm = l.SalesOrder != null && l.SalesOrder.CustomerDeliveryLocation != null ? l.SalesOrder.CustomerDeliveryLocation.LocationName : null,
            Itemcd = l.Item != null ? l.Item.ItemCd : null,
            Jobordmernm = l.Item != null ? l.Item.ItemName : null,
            Jobordqun = l.Quantity,
            Addinfo02 = l.Addinfo != null ? l.Addinfo.Addinfo02 : null
        }).ToList();
    }

    /// <summary>汁仕分表用：喫食日・品目コードで検索し、喫食日・喫食時間・品目でグループ化して返す。</summary>
    public async Task<List<JuiceSearchGroupDto>> SearchByDeliveryDateGroupedAsync(string delvedt, string? itemcd, CancellationToken ct = default)
    {
        var items = await SearchByDeliveryDateAsync(delvedt, itemcd, ct);
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
                Locations = g.Select(x => new JuiceSearchLocationDto
                {
                    Shpctrnm = x.Shpctrnm,
                    Jobordqun = x.Jobordqun,
                    Addinfo02 = x.Addinfo02
                }).ToList()
            })
            .OrderBy(x => x.Delvedt).ThenBy(x => x.ShptmDisplay).ThenBy(x => x.Itemcd)
            .ToList();
        return grouped;
    }

    /// <summary>弁当箱盛り付け指示書（ご飯）用：喫食日・品目コードで検索。itemadditionalinformation.addinfo01 が "1" の品目のみ検索結果に表示。</summary>
    public async Task<List<JobordItemDto>> SearchByDeliveryDateForBentoAsync(string delvedt, string? itemcd, CancellationToken ct = default)
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
            .Include(l => l.OrderTable)
            .Include(l => l.Item!)
                .ThenInclude(i => i!.AdditionalInformation)
            .Where(l => l.PlannedDeliveryDate == delvedtDate)
            .Where(l => l.Item != null
                && l.Item.AdditionalInformation != null
                && l.Item.AdditionalInformation.Addinfo01 == "1");

        if (!string.IsNullOrEmpty(itemcd))
            query = query.Where(l => l.Item != null && l.Item.ItemCd != null && l.Item.ItemCd.Contains(itemcd));

        var linesBento = await query
            .OrderBy(l => l.SalesOrderLineId)
            .ToListAsync(ct);

        return linesBento.Select(l => new JobordItemDto
        {
            Prkey = l.SalesOrderLineId,
            Prddt = l.ProductDate.HasValue ? l.ProductDate.Value.ToString("yyyyMMdd") : null,
            Delvedt = l.PlannedDeliveryDate.HasValue ? l.PlannedDeliveryDate.Value.ToString("yyyyMMdd") : null,
            Shptm = l.Addinfo != null ? l.Addinfo.Addinfo01 : null,
            ShptmName = l.Addinfo != null ? l.Addinfo.Addinfo01Name : null,
            Cuscd = l.SalesOrder != null && l.SalesOrder.Customer != null ? l.SalesOrder.Customer.CustomerCode : null,
            Shpctrcd = l.SalesOrder != null && l.SalesOrder.CustomerDeliveryLocation != null ? l.SalesOrder.CustomerDeliveryLocation.LocationCode : null,
            Shpctrnm = l.SalesOrder != null && l.SalesOrder.CustomerDeliveryLocation != null ? l.SalesOrder.CustomerDeliveryLocation.LocationName : null,
            Itemcd = l.Item != null ? l.Item.ItemCd : null,
            Jobordmernm = l.Item != null ? l.Item.ItemName : null,
            Jobordqun = l.OrderTable != null ? l.OrderTable.Qty : l.Quantity,
            Addinfo02 = l.Addinfo != null ? l.Addinfo.Addinfo02 : null,
            Addinfo01Item = l.Item != null && l.Item.AdditionalInformation != null ? l.Item.AdditionalInformation.Addinfo01 : null,
            Quantity = l.Quantity
        }).ToList();
    }

    /// <summary>弁当箱盛り付け指示書（ご飯）用：喫食日・品目コードで検索し、喫食日・喫食時間・品目でグループ化して返す。</summary>
    public async Task<List<BentoSearchGroupDto>> SearchByDeliveryDateForBentoGroupedAsync(string delvedt, string? itemcd, CancellationToken ct = default)
    {
        var items = await SearchByDeliveryDateForBentoAsync(delvedt, itemcd, ct);
        var keySelector = (JobordItemDto x) => (
            Delvedt: x.Delvedt ?? "",
            ShptmDisplay: x.ShptmName ?? x.Shptm ?? "",
            Itemcd: x.Itemcd ?? "",
            Jobordmernm: x.Jobordmernm ?? ""
        );
        var grouped = items
            .GroupBy(keySelector)
            .Select(g => new BentoSearchGroupDto
            {
                Delvedt = g.Key.Delvedt,
                ShptmDisplay = g.Key.ShptmDisplay,
                Itemcd = g.Key.Itemcd,
                Jobordmernm = g.Key.Jobordmernm,
                Locations = g.Select(x => new BentoSearchLocationDto
                {
                    Shpctrnm = x.Shpctrnm,
                    Jobordqun = x.Jobordqun,
                    Quantity = x.Quantity,
                    Addinfo02 = x.Addinfo02
                }).ToList()
            })
            .OrderBy(x => x.Delvedt).ThenBy(x => x.ShptmDisplay).ThenBy(x => x.Itemcd)
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
                .Include(b => b.ChildItem)
                    .ThenInclude(c => c!.Unit0)
                .Include(b => b.ChildItem)
                    .ThenInclude(c => c!.AdditionalInformation)
                .Include(b => b.ChildItem)
                    .ThenInclude(c => c!.WorkCenterMappings!)
                    .ThenInclude(m => m.Workcenter)
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
            var car0 = addInfo?.Car0 ?? 1m;

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
                Shptm = l.Addinfo?.Addinfo01,
                ShptmName = l.Addinfo?.Addinfo01Name,
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
                SeasoningBoms = seasoningBoms,
                Item = EntityToDtoMapper.ToItemDetailDto(item, addInfo, item?.Unit0, routs),
                Shpctr = EntityToDtoMapper.ToShpctrDetailDto(loc, cust),
                Cusmcd = EntityToDtoMapper.ToCusmcdDetailDto(l.CustomerItem, l.CustomerItem?.Customer),
                Mboms = bomListResolved.Select(t => EntityToDtoMapper.ToMbomDetailDto(t.b, t.child, t.unit)).ToList()
            });
        }

        return rows;
    }

    /// <summary>現品票：ordertable を納期で検索し、同一納期・品目で数量を合算。大分類が指定されていれば絞り込み。</summary>
    public async Task<List<ProductLabelRowDto>> SearchProductLabelAsync(string needdateYyyymmdd, long? majorClassificationId, CancellationToken ct = default)
    {
        var date = ParseProductDate(needdateYyyymmdd);
        if (!date.HasValue)
            throw new ArgumentException("納期はYYYYMMDD形式（8桁）で指定してください。", nameof(needdateYyyymmdd));

        List<ProductLabelSqlRow> raw;
        if (majorClassificationId is long mid and > 0)
        {
            raw = await _db.Database
                .SqlQuery<ProductLabelSqlRow>($@"
            SELECT o.needdate AS ""NeedDate"", i.itemcd AS ""ItemCd"", COALESCE(i.itemname, '') AS ""ItemName"", SUM(COALESCE(o.qty, 0)) AS ""Qty""
            FROM ordertable AS o
            INNER JOIN item AS i ON o.itemcd = i.itemcd
            WHERE o.needdate = {date.Value} AND i.majorclassficationid = {mid}
            GROUP BY o.needdate, i.itemcd, i.itemname
            ORDER BY i.itemcd")
                .ToListAsync(ct);
        }
        else
        {
            raw = await _db.Database
                .SqlQuery<ProductLabelSqlRow>($@"
            SELECT o.needdate AS ""NeedDate"", i.itemcd AS ""ItemCd"", COALESCE(i.itemname, '') AS ""ItemName"", SUM(COALESCE(o.qty, 0)) AS ""Qty""
            FROM ordertable AS o
            INNER JOIN item AS i ON o.itemcd = i.itemcd
            WHERE o.needdate = {date.Value}
            GROUP BY o.needdate, i.itemcd, i.itemname
            ORDER BY i.itemcd")
                .ToListAsync(ct);
        }

        return raw.Select(r => new ProductLabelRowDto
        {
            NeedDate = r.NeedDate.ToString("yyyyMMdd"),
            ItemDisplay = string.IsNullOrEmpty(r.ItemCd)
                ? r.ItemName
                : $"{r.ItemName}（{r.ItemCd}）",
            Qty = r.Qty
        }).ToList();
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

    private static DateOnly? ParseProductDate(string? prddt)
    {
        if (string.IsNullOrEmpty(prddt) || prddt.Length != 8) return null;
        if (int.TryParse(prddt.AsSpan(0, 4), out var y) && int.TryParse(prddt.AsSpan(4, 2), out var m) && int.TryParse(prddt.AsSpan(6, 2), out var d))
            return new DateOnly(y, m, d);
        return null;
    }

}
