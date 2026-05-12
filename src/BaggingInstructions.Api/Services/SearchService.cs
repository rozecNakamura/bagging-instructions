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
            Shptm = l.Addinfo != null ? l.Addinfo.Addinfo04 : null,
            Cuscd = l.SalesOrder != null && l.SalesOrder.Customer != null ? l.SalesOrder.Customer.CustomerCode : null,
            Shpctrcd = l.SalesOrder != null && l.SalesOrder.CustomerDeliveryLocation != null ? l.SalesOrder.CustomerDeliveryLocation.LocationCode : null,
            Itemcd = l.Item != null ? l.Item.ItemCd : null,
            Jobordmernm = l.Item != null ? l.Item.ItemName : null,
            Jobordqun = l.Quantity
        }).ToList();
    }

    /// <summary>袋詰用：製造日・品目コードで受注明細を検索し、製造日×品目で合算したグループを返す。</summary>
    public async Task<List<BaggingSearchGroupDto>> SearchBaggingGroupedAsync(string prddt, string? itemcd, bool? isComplete = null, CancellationToken ct = default)
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

        var registrations = await _db.BaggingInputRegistrations.AsNoTracking()
            .Where(r => r.ProductDate == prddtDate.Value)
            .ToListAsync(ct);
        var printedByItemCd = registrations.ToDictionary(r => r.ItemCode, r => r.IsPrinted);

        var groups = lines
            .Where(l => l.Item != null && !string.IsNullOrEmpty(l.Item.ItemCd))
            .GroupBy(l => l.Item!.ItemCd!)
            .Select(g =>
            {
                var first = g.First();
                var item = first.Item!;
                var printed = printedByItemCd.TryGetValue(g.Key, out var p) && p;
                return new BaggingSearchGroupDto
                {
                    Prddt = prddtDate.Value.ToString("yyyyMMdd"),
                    Itemcd = g.Key,
                    Itemnm = item.ItemName,
                    TotalJobordqun = g.Sum(x => x.Quantity),
                    UnitCode = item.Unit0?.UnitCode,
                    UnitName = item.Unit0?.UnitName,
                    LinePrkeys = g.Select(x => x.SalesOrderLineId).OrderBy(id => id).ToList(),
                    IsPrinted = printed
                };
            })
            .OrderBy(x => x.Itemcd, StringComparer.Ordinal)
            .ToList();

        if (isComplete.HasValue)
            groups = groups.Where(g => g.IsPrinted == isComplete.Value).ToList();

        return groups;
    }

    /// <summary>汁仕分表用：喫食日・品目コードで検索。delvedt は YYYYMMDD、itemcd は部分一致。item.middleclassficationcode が 50 または 51 の品目のみ。</summary>
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
            .Where(l => l.PlannedDeliveryDate == delvedtDate)
            .Where(l => l.Item != null
                && l.Item.MiddleClassificationCode != null
                && (l.Item.MiddleClassificationCode == "50" || l.Item.MiddleClassificationCode == "51"));

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
            Shptm = l.Addinfo != null ? l.Addinfo.Addinfo04 : null,
            ShptmName = l.Addinfo != null ? l.Addinfo.Addinfo04Name : null,
            Cuscd = l.SalesOrder != null && l.SalesOrder.Customer != null ? l.SalesOrder.Customer.CustomerCode : null,
            Shpctrcd = l.SalesOrder != null && l.SalesOrder.CustomerDeliveryLocation != null ? l.SalesOrder.CustomerDeliveryLocation.LocationCode : null,
            Shpctrnm = l.SalesOrder != null && l.SalesOrder.CustomerDeliveryLocation != null ? l.SalesOrder.CustomerDeliveryLocation.LocationName : null,
            Itemcd = l.Item != null ? l.Item.ItemCd : null,
            Jobordmernm = l.Item != null ? l.Item.ItemName : null,
            Jobordqun = l.Quantity,
            Addinfo01 = l.Addinfo != null ? l.Addinfo.Addinfo01 : null
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
                    Addinfo01 = x.Addinfo01
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
                    Addinfo01 = x.Addinfo01
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
                ShptmName = l.Addinfo?.Addinfo04Name,
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
                sql.AppendLine("AND i.majorclassficationcode = @majorCode");
                cmd.Parameters.AddWithValue("majorCode", majorCode);
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

            var list = new List<ProductLabelRowDto>();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                list.Add(new ProductLabelRowDto
                {
                    OrderTableId = reader.GetInt64(0),
                    ReleaseDate = ReadDateOnlyNullable(reader, 1)?.ToString("yyyyMMdd") ?? "",
                    ItemCode = reader.GetString(2),
                    ItemName = reader.GetString(3),
                    Qty = reader.GetDecimal(4),
                    WorkcenterName = reader.GetString(5),
                    ChildCount = (int)reader.GetInt64(6),
                });
            }
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

            return list;
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
            return list;
        }
        finally
        {
            if (shouldClose && conn.State == ConnectionState.Open)
                await conn.CloseAsync();
        }
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

    private static DateOnly? ParseProductDate(string? prddt)
    {
        if (string.IsNullOrEmpty(prddt) || prddt.Length != 8) return null;
        if (int.TryParse(prddt.AsSpan(0, 4), out var y) && int.TryParse(prddt.AsSpan(4, 2), out var m) && int.TryParse(prddt.AsSpan(6, 2), out var d))
            return new DateOnly(y, m, d);
        return null;
    }

}
