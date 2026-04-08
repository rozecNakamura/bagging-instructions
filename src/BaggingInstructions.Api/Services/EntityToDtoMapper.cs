using BaggingInstructions.Api.Entities.Legacy;
using BaggingInstructions.Api.DTOs;

namespace BaggingInstructions.Api.Services;

public static partial class EntityToDtoMapper
{
    public static UniDetailDto? ToUniDetailDto(Uni? u)
    {
        if (u == null) return null;
        return new UniDetailDto { Prkey = u.Prkey, Unicd = u.Unicd, Uninm = u.Uninm, Uniinfnm = u.Uniinfnm, Dispno = u.Dispno };
    }

    public static WareDetailDto? ToWareDetailDto(Ware? w)
    {
        if (w == null) return null;
        return new WareDetailDto { Prkey = w.Prkey, Fctcd = w.Fctcd, Whcd = w.Whcd, Whnm = w.Whnm, Whinfnm = w.Whinfnm, Zip = w.Zip, Add1 = w.Add1, Add2 = w.Add2, Email = w.Email, Tel = w.Tel, Fax = w.Fax, Whpicnm = w.Whpicnm, Deptcd = w.Deptcd, Linecd = w.Linecd };
    }

    public static WorkcDetailDto? ToWorkcDetailDto(Workc? w)
    {
        if (w == null) return null;
        return new WorkcDetailDto { Prkey = w.Prkey, Fctcd = w.Fctcd, Wccd = w.Wccd, Wcnm = w.Wcnm, Wcinfnm = w.Wcinfnm, Stdcap = w.Stdcap, Manrate = w.Manrate, Capacity = w.Capacity, Caprate = w.Caprate, Statm = w.Statm, Endtm = w.Endtm, Deptcd = w.Deptcd, Wcwhcd = w.Wcwhcd };
    }

    public static RoutDetailDto ToRoutDetailDto(Rout r, Ware? ware, Workc? workc)
    {
        return new RoutDetailDto
        {
            Prkey = r.Prkey,
            Fctcd = r.Fctcd,
            Deptcd = r.Deptcd,
            Itemgr = r.Itemgr,
            Itemcd = r.Itemcd,
            Linecd = r.Linecd,
            Routno = (int)r.Routno,
            Whcd = r.Whcd,
            Loccd = r.Loccd,
            Prccd = r.Prccd,
            Prclt = r.Prclt,
            Prccap = r.Prccap,
            Arngtm = r.Arngtm,
            Proctm = r.Proctm,
            Unitprice = r.Unitprice,
            Unitprice1 = r.Unitprice1,
            Unitprice2 = r.Unitprice2,
            Unitprice3 = r.Unitprice3,
            Actcd = r.Actcd,
            Manjor = r.Manjor,
            Routstdcos = r.Routstdcos,
            Berthcd = r.Berthcd,
            Prcpes = r.Prcpes,
            Defpresetupmemo = r.Defpresetupmemo,
            Ware = ToWareDetailDto(ware),
            Workc = ToWorkcDetailDto(workc)
        };
    }

    public static ItemDetailDto? ToItemDetailDto(ItemLegacy? i, IReadOnlyList<Rout>? routs = null)
    {
        if (i == null) return null;
        var routDtos = (routs ?? new List<Rout>())
            .Select(r => ToRoutDetailDto(r, r.Ware, r.Workc)).ToList();
        return new ItemDetailDto
        {
            Prkey = i.Prkey,
            Fctcd = i.Fctcd,
            Deptcd = i.Deptcd,
            Itemgr = i.Itemgr,
            Itemcd = i.Itemcd ?? "",
            Itemnm = i.Itemnm ?? "",
            Std1 = i.Std,
            Std2 = null,
            Std3 = null,
            Uni0 = i.Uni0,
            Nwei = i.Nwei,
            Jouni = i.Jouni,
            Strtemp = i.Strtemp,
            Steritemprange = i.Strtemp,
            Steritime = null,
            Kikunip = i.Car,
            Classification1Code = null,
            Classification2Code = null,
            Classification3Code = null,
            IsLiquid = ItemCodeKind.IsLiquid(i.Itemcd),
            Uni = null,
            Routs = routDtos
        };
    }

    public static ShpctrDetailDto? ToShpctrDetailDto(Shpctr? s)
    {
        if (s == null) return null;
        return new ShpctrDetailDto { Prkey = s.Prkey, Fctcd = s.Fctcd, Cuscd = s.Cuscd, Shpctrcd = s.Shpctrcd, Shpctrnm = s.Shpctrnm ?? "", Shpctrabb = s.Shpctrabb, Zip = s.Zip, Add1 = s.Add1, Add2 = s.Add2, Email = s.Email, Tel = s.Tel, Fax = s.Fax, Linecd = s.Linecd, Dispno = s.Dispno };
    }

    public static MbomDetailDto ToMbomDetailDto(Mbom m)
    {
        return new MbomDetailDto
        {
            Prkey = m.Prkey,
            Pfctcd = m.Pfctcd,
            Pdeptcd = m.Pdeptcd,
            Pitemgr = m.Pitemgr,
            Pitemcd = m.Pitemcd,
            Proutno = m.Proutno,
            Cfctcd = m.Cfctcd,
            Cdeptcd = m.Cdeptcd,
            Citemgr = m.Citemgr,
            Citemcd = m.Citemcd ?? "",
            Amu = m.Amu,
            Otp = m.Otp,
            Partyp = m.Partyp,
            Par = m.Par,
            Prvtyp = m.Prvtyp,
            Issjor = m.Issjor,
            Memo = m.Memo,
            Stadt = m.Stadt,
            Enddt = m.Enddt,
            ChildItem = ToItemDetailDto(m.ChildItem)
        };
    }

    public static CusmcdDetailDto? ToCusmcdDetailDto(Cusmcd? c)
    {
        if (c == null) return null;
        return new CusmcdDetailDto { Prkey = c.Prkey, Merfctcd = c.Merfctcd, Cuscd = c.Cuscd, Cusitemcd = c.Cusitemcd, Cusitemnm = c.Cusitemnm ?? "", Fctcd = c.Fctcd, Deptcd = c.Deptcd, Itemgr = c.Itemgr, Itemcd = c.Itemcd };
    }

    public static JobordDetailItemDto ToJobordDetailItemDto(Jobord j)
    {
        return new JobordDetailItemDto
        {
            Prkey = j.Prkey,
            Jobordno = j.Jobordno,
            Jobordsno = j.Jobordsno,
            Prddt = j.Prddt,
            Delvedt = j.Delvedt,
            Shptm = j.Shptm,
            Itemcd = j.Itemcd,
            Cuscd = j.Cuscd,
            Shpctrcd = j.Shpctrcd,
            Cusitemcd = j.Cusitemcd,
            Jobordqun = j.Jobordqun,
            Linecd = j.Linecd,
            Jobordmernm = j.Jobordmernm,
            Item = ToItemDetailDto(j.Item),
            Shpctr = ToShpctrDetailDto(j.Shpctr),
            Routs = (j.Item?.Routs ?? new List<Rout>()).Select(r => ToRoutDetailDto(r, r.Ware, r.Workc)).ToList(),
            Mboms = j.Mboms.Select(ToMbomDetailDto).ToList(),
            Cusmcd = ToCusmcdDetailDto(j.Cusmcd)
        };
    }
}
