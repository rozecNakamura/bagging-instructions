using System.Linq;
using BaggingInstructions.Api.Entities;
using BaggingInstructions.Api.DTOs;

namespace BaggingInstructions.Api.Services;

/// <summary>新DB（craftlineax）エンティティから既存 DTO へのマッピング（API 契約維持）</summary>
public static partial class EntityToDtoMapper
{
    public static UniDetailDto? ToUniDetailDto(Unit? u)
    {
        if (u == null) return null;
        return new UniDetailDto
        {
            Prkey = u.UnitId ?? 0,
            Unicd = u.UnitCode,
            Uninm = u.UnitName,
            Uniinfnm = u.UnitSymbol,
            Dispno = u.SortOrder
        };
    }

    public static RoutDetailDto ToRoutDetailDto(ItemWorkCenterMapping m, Workcenter? w)
    {
        return new RoutDetailDto
        {
            Prkey = w?.WorkcenterId ?? 0,
            Fctcd = null,
            Deptcd = null,
            Itemgr = null,
            Itemcd = m.ItemCd ?? "",
            Linecd = null,
            Routno = 0,
            Whcd = null,
            Loccd = null,
            Prccd = null,
            Prclt = null,
            Prccap = null,
            Arngtm = null,
            Proctm = null,
            Unitprice = null,
            Unitprice1 = 0,
            Unitprice2 = 0,
            Unitprice3 = 0,
            Actcd = null,
            Manjor = null,
            Routstdcos = null,
            Berthcd = null,
            Prcpes = 0,
            Defpresetupmemo = null,
            Ware = null,
            Workc = w == null ? null : new WorkcDetailDto
            {
                Prkey = w.WorkcenterId ?? 0,
                Fctcd = null,
                Wccd = w.WorkcenterCode,
                Wcnm = w.WorkcenterName,
                Wcinfnm = null,
                Stdcap = null,
                Manrate = null,
                Capacity = null,
                Caprate = null,
                Statm = null,
                Endtm = null,
                Deptcd = null,
                Wcwhcd = null
            }
        };
    }

    public static ItemDetailDto? ToItemDetailDto(Item? i, ItemAdditionalInformation? addInfo, Unit? unit0, IReadOnlyList<(ItemWorkCenterMapping m, Workcenter? w)>? routs = null)
    {
        if (i == null) return null;
        var routDtos = (routs ?? new List<(ItemWorkCenterMapping, Workcenter?)>())
            .Select(x => ToRoutDetailDto(x.m, x.w)).ToList();
        return new ItemDetailDto
        {
            Prkey = i.ItemId ?? 0,
            Fctcd = null,
            Deptcd = null,
            Itemgr = null,
            Itemcd = i.ItemCd ?? "",
            Itemnm = i.ItemName ?? "",
            Std1 = addInfo?.Std1,
            Std2 = addInfo?.Std2,
            Std3 = addInfo?.Std3,
            Uni0 = unit0?.UnitCode,
            // Nwei = addInfo?.Nwei,
            Jouni = i.ShortName,
            Strtemp = addInfo?.SterItemPrange?.ToString(),
            Steritemprange = addInfo?.SterItemPrange?.ToString(),
            Steritime = addInfo?.SteriTime,
            Kikunip = addInfo?.Car0,
            Classification1Code = i.Classification1Code,
            Classification2Code = i.Classification2Code,
            Classification3Code = i.Classification3Code,
            IsLiquid = ItemCodeKind.IsLiquid(i.ItemCd),
            Uni = ToUniDetailDto(unit0),
            Routs = routDtos
        };
    }

    public static ShpctrDetailDto? ToShpctrDetailDto(CustomerDeliveryLocation? loc, Customer? customer)
    {
        if (loc == null) return null;
        return new ShpctrDetailDto
        {
            Prkey = loc.DeliveryLocationId,
            Fctcd = null,
            Cuscd = customer?.CustomerCode,
            Shpctrcd = loc.LocationCode ?? "",
            Shpctrnm = loc.LocationName ?? "",
            Shpctrabb = loc.LocationShortName,
            Zip = loc.PostalCode,
            Add1 = loc.Address1,
            Add2 = loc.Address2,
            Email = null,
            Tel = loc.PhoneNumber,
            Fax = loc.FaxNumber,
            Linecd = null,
            Dispno = loc.SortOrder
        };
    }

    public static MbomDetailDto ToMbomDetailDto(Bom b, Item? childItem, Unit? childUnit)
    {
        return new MbomDetailDto
        {
            Prkey = b.BomId,
            Pfctcd = null,
            Pdeptcd = null,
            Pitemgr = null,
            Pitemcd = b.ParentItemCd ?? "",
            Proutno = b.ProductionOrder ?? 0,
            Cfctcd = null,
            Cdeptcd = null,
            Citemgr = null,
            Citemcd = b.ChildItemCd ?? "",
            Amu = b.InputQty,
            Otp = b.OutputQty,
            Partyp = null,
            Par = null,
            Prvtyp = null,
            Issjor = null,
            Memo = b.Memo,
            Stadt = b.StartDate?.ToString(),
            Enddt = b.EndDate?.ToString(),
            ChildItem = ToItemDetailDto(
                childItem,
                childItem?.AdditionalInformation,
                childUnit,
                childItem?.WorkCenterMappings is { Count: > 0 } wm
                    ? wm.Select(m => (m, m.Workcenter)).ToList()
                    : null)
        };
    }

    public static CusmcdDetailDto? ToCusmcdDetailDto(CustomerItem? ci, Customer? customer)
    {
        if (ci == null) return null;
        return new CusmcdDetailDto
        {
            Prkey = DbTextId.ToInt64(ci.CustomerItemId),
            Merfctcd = null,
            Cuscd = customer?.CustomerCode,
            Cusitemcd = ci.CustomerCode ?? "",
            Cusitemnm = ci.CustomerName ?? ci.CustomerShortName ?? "",
            Fctcd = null,
            Deptcd = null,
            Itemgr = null,
            Itemcd = null
        };
    }

    public static JobordDetailItemDto ToJobordDetailItemDto(BaggingDetailRow row)
    {
        return new JobordDetailItemDto
        {
            Prkey = row.Prkey,
            Jobordno = row.Jobordno ?? "",
            Jobordsno = 0,
            Prddt = row.Prddt,
            Delvedt = row.Delvedt,
            Shptm = row.Shptm,
            Itemcd = row.Itemcd,
            Cuscd = row.Cuscd,
            Shpctrcd = row.Shpctrcd,
            Cusitemcd = row.Cusmcd?.Cusitemcd,
            Jobordqun = row.Jobordqun,
            Linecd = null,
            Jobordmernm = row.Jobordmernm,
            Item = row.Item,
            Shpctr = row.Shpctr,
            Routs = row.Item?.Routs ?? new List<RoutDetailDto>(),
            Mboms = row.Mboms,
            Cusmcd = row.Cusmcd
        };
    }

}
