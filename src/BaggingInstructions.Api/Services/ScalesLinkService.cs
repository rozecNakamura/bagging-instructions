using System.Globalization;
using System.Text;
using BaggingInstructions.Api.Core;
using BaggingInstructions.Api.DTOs;
using BaggingInstructions.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace BaggingInstructions.Api.Services;

public class ScalesLinkService
{
    private readonly AppDbContext _db;

    public ScalesLinkService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ScalesLinkOrdersResponseDto> SearchOrdersAsync(
        DateOnly? releaseDateFrom,
        DateOnly? releaseDateTo,
        CancellationToken ct)
    {
        if (releaseDateFrom.HasValue && releaseDateTo.HasValue && releaseDateFrom.Value > releaseDateTo.Value)
            throw new ArgumentException("着手日の開始日は終了日以前を指定してください");

        var query =
            from o in _db.OrderTables.AsNoTracking()
            join ai in _db.ItemAdditionalInformations.AsNoTracking()
                on o.ItemCode equals ai.ItemCd
            where (ai.Addinfo06 ?? "").Trim() != ""
                  && (releaseDateFrom == null || o.ReleaseDate >= releaseDateFrom)
                  && (releaseDateTo == null || o.ReleaseDate <= releaseDateTo)
            orderby o.ReleaseDate, o.OrderTableId ?? o.SalesOrderLineId
            select new ScalesLinkOrderRowDto
            {
                Ordertableid = o.OrderTableId ?? o.SalesOrderLineId,
                Itemcode = o.ItemCode ?? "",
                Addinfo06 = ai.Addinfo06!.Trim(),
                Releasedate = o.ReleaseDate,
                Workcentercode = o.WorkCenterCode,
                Qty = o.Qty
            };

        var list = await query.ToListAsync(ct);
        return new ScalesLinkOrdersResponseDto { TotalCount = list.Count, Orders = list };
    }

    public async Task<byte[]> BuildItemCsvAsync(CancellationToken ct)
    {
        var rows = await (
                from i in _db.Items.AsNoTracking()
                join ai in _db.ItemAdditionalInformations.AsNoTracking()
                    on i.ItemCd equals ai.ItemCd
                where (ai.Addinfo06 ?? "").Trim() != ""
                orderby i.ItemCd
                select new
                {
                    Itemcd = i.ItemCd,
                    Addinfo06 = (ai.Addinfo06 ?? "").Trim(),
                    i.ItemName,
                    Loccd = i.SupplierCode != null && i.SupplierCode.Trim() != ""
                        ? i.SupplierCode.Trim()
                        : (i.WorkCenterCode ?? "").Trim(),
                    Whcd = i.WarehouseCode ?? "",
                    i.UnitCode0,
                    i.UnitCode1,
                    i.UnitCode2,
                    i.UnitCode3,
                    i.ConversionValue1,
                    i.ConversionValue2,
                    i.ConversionValue3
                })
            .ToListAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine("ITEMCD,ADDINFO06,ITEMNM,LOCCD,WHCD,UNI0,UNI1,UNI2,UNI3,UNICON1,UNICON2,UNICON3");
        foreach (var r in rows)
        {
            sb.Append(Csv(r.Itemcd)).Append(',')
                .Append(Csv(r.Addinfo06)).Append(',')
                .Append(Csv(r.ItemName)).Append(',')
                .Append(Csv(r.Loccd)).Append(',')
                .Append(Csv(r.Whcd)).Append(',')
                .Append(Csv(r.UnitCode0 ?? "")).Append(',')
                .Append(Csv(r.UnitCode1 ?? "")).Append(',')
                .Append(Csv(r.UnitCode2 ?? "")).Append(',')
                .Append(Csv(r.UnitCode3 ?? "")).Append(',')
                .Append(Num(r.ConversionValue1)).Append(',')
                .Append(Num(r.ConversionValue2)).Append(',')
                .AppendLine(Num(r.ConversionValue3));
        }

        return EncodeUtf8NoBom(sb);
    }

    public async Task<byte[]> BuildMbomCsvAsync(CancellationToken ct)
    {
        // bom は親・子とも item に存在する行のみ対象。
        // PITEMCD/CITEMCD は addinfo06 があればそれを使い、無ければ itemcode（計量器未登録でも BOM を出力する）
        var rows = await (
                from b in _db.Boms.AsNoTracking()
                join ip in _db.Items.AsNoTracking() on b.ParentItemCd equals ip.ItemCd
                join ic in _db.Items.AsNoTracking() on b.ChildItemCd equals ic.ItemCd
                join aip in _db.ItemAdditionalInformations.AsNoTracking()
                    on ip.ItemCd equals aip.ItemCd into aipGrp
                from aip in aipGrp.DefaultIfEmpty()
                join aic in _db.ItemAdditionalInformations.AsNoTracking()
                    on ic.ItemCd equals aic.ItemCd into aicGrp
                from aic in aicGrp.DefaultIfEmpty()
                orderby ip.ItemCd, ic.ItemCd
                select new
                {
                    Pitemcd = aip != null && (aip.Addinfo06 ?? "").Trim() != ""
                        ? (aip.Addinfo06 ?? "").Trim()
                        : ip.ItemCd,
                    Citemcd = aic != null && (aic.Addinfo06 ?? "").Trim() != ""
                        ? (aic.Addinfo06 ?? "").Trim()
                        : ic.ItemCd,
                    b.InputQty,
                    b.OutputQty,
                    b.YieldPercent,
                    b.Memo
                })
            .ToListAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine("PITEMCD,CITEMCD,AMU,OTP,YIELDPCT,MEMO");
        foreach (var r in rows)
        {
            sb.Append(Csv(r.Pitemcd)).Append(',')
                .Append(Csv(r.Citemcd)).Append(',')
                .Append(Num(r.InputQty)).Append(',')
                .Append(Num(r.OutputQty)).Append(',')
                .Append(Num(r.YieldPercent)).Append(',')
                .AppendLine(Csv(r.Memo ?? ""));
        }

        return EncodeUtf8NoBom(sb);
    }

    public async Task<byte[]> BuildOrderCsvAsync(
        DateOnly? releaseDateFrom,
        DateOnly? releaseDateTo,
        CancellationToken ct)
    {
        if (releaseDateFrom.HasValue && releaseDateTo.HasValue && releaseDateFrom.Value > releaseDateTo.Value)
            throw new ArgumentException("着手日の開始日は終了日以前を指定してください");

        var rows = await (
                from o in _db.OrderTables.AsNoTracking()
                join aiParent in _db.ItemAdditionalInformations.AsNoTracking()
                    on o.ItemCode equals aiParent.ItemCd
                join b in _db.Boms.AsNoTracking()
                    on o.ItemCode equals b.ParentItemCd
                join aiChild in _db.ItemAdditionalInformations.AsNoTracking()
                    on b.ChildItemCd equals aiChild.ItemCd into aiChildGrp
                from aiChild in aiChildGrp.DefaultIfEmpty()
                join childItem in _db.Items.AsNoTracking()
                    on b.ChildItemCd equals childItem.ItemCd into childItemGrp
                from childItem in childItemGrp.DefaultIfEmpty()
                join u in _db.Units.AsNoTracking()
                    on childItem.UnitCode0 equals u.UnitCode into uGrp
                from u in uGrp.DefaultIfEmpty()
                where (aiParent.Addinfo06 ?? "").Trim() != ""
                      && (releaseDateFrom == null || o.ReleaseDate >= releaseDateFrom)
                      && (releaseDateTo == null || o.ReleaseDate <= releaseDateTo)
                orderby o.ReleaseDate, o.OrderTableId ?? o.SalesOrderLineId, b.ChildItemCd
                select new OrderCsvRow(
                    o.OrderTableId ?? o.SalesOrderLineId,
                    (aiParent.Addinfo06 ?? "").Trim(),
                    o.ReleaseDate,
                    o.WorkCenterCode,
                    aiChild != null ? (aiChild.Addinfo06 ?? "").Trim() : "",
                    b.InputQty,
                    u != null ? u.UnitCode : ""))
            .ToListAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine("orderid,itemcd,plandate,workcenterid,chlditemcd,reqpty,unitid");
        foreach (var r in rows)
        {
            var plan = r.ReleaseDate.HasValue
                ? $"{r.ReleaseDate.Value.Year}/{r.ReleaseDate.Value.Month}/{r.ReleaseDate.Value.Day}"
                : "";
            sb.Append(Csv(r.OrderId.ToString(CultureInfo.InvariantCulture))).Append(',')
                .Append(Csv(r.ParentScaleItemCd)).Append(',')
                .Append(Csv(plan)).Append(',')
                .Append(Csv(r.WorkCenterCode ?? "")).Append(',')
                .Append(Csv(r.ChildScaleItemCd)).Append(',')
                .Append(Num(r.ReqQty)).Append(',')
                .AppendLine(Csv(r.UnitCode));
        }

        return EncodeUtf8NoBom(sb);
    }

    private sealed record OrderCsvRow(
        long OrderId,
        string ParentScaleItemCd,
        DateOnly? ReleaseDate,
        string? WorkCenterCode,
        string ChildScaleItemCd,
        decimal ReqQty,
        string UnitCode);

    private static string Csv(string? value)
    {
        var s = value ?? "";
        if (s.Contains('"', StringComparison.Ordinal))
            s = s.Replace("\"", "\"\"", StringComparison.Ordinal);
        if (s.Contains(',', StringComparison.Ordinal) || s.Contains('\r') || s.Contains('\n') ||
            s.Contains('"', StringComparison.Ordinal))
            return $"\"{s}\"";
        return s;
    }

    private static string Num(decimal? v) => (v ?? 0m).ToString(CultureInfo.InvariantCulture);

    private static byte[] EncodeUtf8NoBom(StringBuilder sb)
    {
        var enc = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        var t = sb.ToString();
        t = t.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", "\r\n", StringComparison.Ordinal);
        return enc.GetBytes(t);
    }
}
