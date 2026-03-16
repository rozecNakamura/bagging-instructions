using System.Text.Json.Serialization;

namespace BaggingInstructions.Api.DTOs;

public class JobordItemDto
{
    [JsonPropertyName("prkey")]
    public long Prkey { get; set; }

    [JsonPropertyName("prddt")]
    public string? Prddt { get; set; }

    [JsonPropertyName("delvedt")]
    public string? Delvedt { get; set; }

    [JsonPropertyName("shptm")]
    public string? Shptm { get; set; }

    /// <summary>喫食時間表示用（addinfo01name）。汁仕分表で使用。</summary>
    [JsonPropertyName("shptm_name")]
    public string? ShptmName { get; set; }

    [JsonPropertyName("cuscd")]
    public string? Cuscd { get; set; }

    [JsonPropertyName("shpctrcd")]
    public string? Shpctrcd { get; set; }

    [JsonPropertyName("itemcd")]
    public string? Itemcd { get; set; }

    [JsonPropertyName("jobordmernm")]
    public string? Jobordmernm { get; set; }

    [JsonPropertyName("jobordqun")]
    public decimal Jobordqun { get; set; }

    /// <summary>受注数量（SalesOrderLine.Quantity）。弁当箱盛り付け指示書（ご飯）の GRAM＝quantity/addinfo02 用。</summary>
    [JsonPropertyName("quantity")]
    public decimal Quantity { get; set; }

    /// <summary>納入場所名。汁仕分表テンプレート用。</summary>
    [JsonPropertyName("shpctrnm")]
    public string? Shpctrnm { get; set; }

    /// <summary>食数計算用除数（addinfo02）。汁仕分表テンプレート用。</summary>
    [JsonPropertyName("addinfo02")]
    public string? Addinfo02 { get; set; }

    /// <summary>品目付加情報 addinfo01（ご飯量等）。弁当箱盛り付け指示書（ご飯）の GRAM 用。</summary>
    [JsonPropertyName("addinfo01_item")]
    public string? Addinfo01Item { get; set; }
}

public class SearchResponseDto
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("items")]
    public List<JobordItemDto> Items { get; set; } = new();
}

// 詳細検索用ネスト DTO（API 契約の snake_case）
public class UniDetailDto
{
    [JsonPropertyName("prkey")]
    public long Prkey { get; set; }

    [JsonPropertyName("unicd")]
    public string? Unicd { get; set; }

    [JsonPropertyName("uninm")]
    public string? Uninm { get; set; }

    [JsonPropertyName("uniinfnm")]
    public string? Uniinfnm { get; set; }

    [JsonPropertyName("dispno")]
    public decimal? Dispno { get; set; }
}

public class WareDetailDto
{
    [JsonPropertyName("prkey")]
    public long Prkey { get; set; }

    [JsonPropertyName("fctcd")]
    public string? Fctcd { get; set; }

    [JsonPropertyName("whcd")]
    public string? Whcd { get; set; }

    [JsonPropertyName("whnm")]
    public string? Whnm { get; set; }

    [JsonPropertyName("whinfnm")]
    public string? Whinfnm { get; set; }

    [JsonPropertyName("zip")]
    public string? Zip { get; set; }

    [JsonPropertyName("add1")]
    public string? Add1 { get; set; }

    [JsonPropertyName("add2")]
    public string? Add2 { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("tel")]
    public string? Tel { get; set; }

    [JsonPropertyName("fax")]
    public string? Fax { get; set; }

    [JsonPropertyName("whpicnm")]
    public string? Whpicnm { get; set; }

    [JsonPropertyName("deptcd")]
    public string? Deptcd { get; set; }

    [JsonPropertyName("linecd")]
    public string? Linecd { get; set; }
}

public class WorkcDetailDto
{
    [JsonPropertyName("prkey")]
    public long Prkey { get; set; }

    [JsonPropertyName("fctcd")]
    public string? Fctcd { get; set; }

    [JsonPropertyName("wccd")]
    public string? Wccd { get; set; }

    [JsonPropertyName("wcnm")]
    public string? Wcnm { get; set; }

    [JsonPropertyName("wcinfnm")]
    public string? Wcinfnm { get; set; }

    [JsonPropertyName("stdcap")]
    public decimal? Stdcap { get; set; }

    [JsonPropertyName("manrate")]
    public decimal? Manrate { get; set; }

    [JsonPropertyName("capacity")]
    public decimal? Capacity { get; set; }

    [JsonPropertyName("caprate")]
    public decimal? Caprate { get; set; }

    [JsonPropertyName("statm")]
    public string? Statm { get; set; }

    [JsonPropertyName("endtm")]
    public string? Endtm { get; set; }

    [JsonPropertyName("deptcd")]
    public string? Deptcd { get; set; }

    [JsonPropertyName("wcwhcd")]
    public string? Wcwhcd { get; set; }
}

public class RoutDetailDto
{
    [JsonPropertyName("prkey")]
    public long Prkey { get; set; }

    [JsonPropertyName("fctcd")]
    public string? Fctcd { get; set; }

    [JsonPropertyName("deptcd")]
    public string? Deptcd { get; set; }

    [JsonPropertyName("itemgr")]
    public string? Itemgr { get; set; }

    [JsonPropertyName("itemcd")]
    public string? Itemcd { get; set; }

    [JsonPropertyName("linecd")]
    public string? Linecd { get; set; }

    [JsonPropertyName("routno")]
    public int Routno { get; set; }

    [JsonPropertyName("whcd")]
    public string? Whcd { get; set; }

    [JsonPropertyName("loccd")]
    public string? Loccd { get; set; }

    [JsonPropertyName("prccd")]
    public string? Prccd { get; set; }

    [JsonPropertyName("prclt")]
    public decimal? Prclt { get; set; }

    [JsonPropertyName("prccap")]
    public decimal? Prccap { get; set; }

    [JsonPropertyName("arngtm")]
    public decimal? Arngtm { get; set; }

    [JsonPropertyName("proctm")]
    public decimal? Proctm { get; set; }

    [JsonPropertyName("prctm")]
    public decimal? Prctm { get; set; }

    [JsonPropertyName("unitprice")]
    public decimal? Unitprice { get; set; }

    [JsonPropertyName("unitprice1")]
    public decimal? Unitprice1 { get; set; }

    [JsonPropertyName("unitprice2")]
    public decimal? Unitprice2 { get; set; }

    [JsonPropertyName("unitprice3")]
    public decimal? Unitprice3 { get; set; }

    [JsonPropertyName("actcd")]
    public string? Actcd { get; set; }

    [JsonPropertyName("manjor")]
    public string? Manjor { get; set; }

    [JsonPropertyName("routstdcos")]
    public decimal? Routstdcos { get; set; }

    [JsonPropertyName("berthcd")]
    public string? Berthcd { get; set; }

    [JsonPropertyName("prcpes")]
    public decimal? Prcpes { get; set; }

    [JsonPropertyName("defpresetupmemo")]
    public string? Defpresetupmemo { get; set; }

    [JsonPropertyName("ware")]
    public WareDetailDto? Ware { get; set; }

    [JsonPropertyName("workc")]
    public WorkcDetailDto? Workc { get; set; }
}

public class ItemDetailDto
{
    [JsonPropertyName("prkey")]
    public long Prkey { get; set; }

    [JsonPropertyName("fctcd")]
    public string? Fctcd { get; set; }

    [JsonPropertyName("deptcd")]
    public string? Deptcd { get; set; }

    [JsonPropertyName("itemgr")]
    public string? Itemgr { get; set; }

    [JsonPropertyName("itemcd")]
    public string Itemcd { get; set; } = "";

    [JsonPropertyName("itemnm")]
    public string Itemnm { get; set; } = "";

    [JsonPropertyName("std")]
    public string? Std { get; set; }

    [JsonPropertyName("uni0")]
    public string? Uni0 { get; set; }

    [JsonPropertyName("nwei")]
    public decimal? Nwei { get; set; }

    [JsonPropertyName("jouni")]
    public string? Jouni { get; set; }

    [JsonPropertyName("strtemp")]
    public string? Strtemp { get; set; }

    [JsonPropertyName("kikunip")]
    public decimal? Kikunip { get; set; }

    [JsonPropertyName("uni")]
    public UniDetailDto? Uni { get; set; }

    [JsonPropertyName("routs")]
    public List<RoutDetailDto> Routs { get; set; } = new();
}

public class ShpctrDetailDto
{
    [JsonPropertyName("prkey")]
    public long Prkey { get; set; }

    [JsonPropertyName("fctcd")]
    public string? Fctcd { get; set; }

    [JsonPropertyName("cuscd")]
    public string? Cuscd { get; set; }

    [JsonPropertyName("shpctrcd")]
    public string Shpctrcd { get; set; } = "";

    [JsonPropertyName("shpctrnm")]
    public string Shpctrnm { get; set; } = "";

    [JsonPropertyName("shpctrabb")]
    public string? Shpctrabb { get; set; }

    [JsonPropertyName("zip")]
    public string? Zip { get; set; }

    [JsonPropertyName("add1")]
    public string? Add1 { get; set; }

    [JsonPropertyName("add2")]
    public string? Add2 { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("tel")]
    public string? Tel { get; set; }

    [JsonPropertyName("fax")]
    public string? Fax { get; set; }

    [JsonPropertyName("linecd")]
    public string? Linecd { get; set; }

    [JsonPropertyName("dispno")]
    public decimal? Dispno { get; set; }
}

public class MbomDetailDto
{
    [JsonPropertyName("prkey")]
    public long Prkey { get; set; }

    [JsonPropertyName("pfctcd")]
    public string? Pfctcd { get; set; }

    [JsonPropertyName("pdeptcd")]
    public string? Pdeptcd { get; set; }

    [JsonPropertyName("pitemgr")]
    public string? Pitemgr { get; set; }

    [JsonPropertyName("pitemcd")]
    public string? Pitemcd { get; set; }

    [JsonPropertyName("proutno")]
    public decimal? Proutno { get; set; }

    [JsonPropertyName("cfctcd")]
    public string? Cfctcd { get; set; }

    [JsonPropertyName("cdeptcd")]
    public string? Cdeptcd { get; set; }

    [JsonPropertyName("citemgr")]
    public string? Citemgr { get; set; }

    [JsonPropertyName("citemcd")]
    public string Citemcd { get; set; } = "";

    [JsonPropertyName("amu")]
    public decimal? Amu { get; set; }

    [JsonPropertyName("otp")]
    public decimal? Otp { get; set; }

    [JsonPropertyName("partyp")]
    public string? Partyp { get; set; }

    [JsonPropertyName("par")]
    public decimal? Par { get; set; }

    [JsonPropertyName("prvtyp")]
    public string? Prvtyp { get; set; }

    [JsonPropertyName("issjor")]
    public string? Issjor { get; set; }

    [JsonPropertyName("memo")]
    public string? Memo { get; set; }

    [JsonPropertyName("stadt")]
    public string? Stadt { get; set; }

    [JsonPropertyName("enddt")]
    public string? Enddt { get; set; }

    [JsonPropertyName("child_item")]
    public ItemDetailDto? ChildItem { get; set; }
}

public class CusmcdDetailDto
{
    [JsonPropertyName("prkey")]
    public long Prkey { get; set; }

    [JsonPropertyName("merfctcd")]
    public string? Merfctcd { get; set; }

    [JsonPropertyName("cuscd")]
    public string? Cuscd { get; set; }

    [JsonPropertyName("cusitemcd")]
    public string Cusitemcd { get; set; } = "";

    [JsonPropertyName("cusitemnm")]
    public string Cusitemnm { get; set; } = "";

    [JsonPropertyName("fctcd")]
    public string? Fctcd { get; set; }

    [JsonPropertyName("deptcd")]
    public string? Deptcd { get; set; }

    [JsonPropertyName("itemgr")]
    public string? Itemgr { get; set; }

    [JsonPropertyName("itemcd")]
    public string? Itemcd { get; set; }
}

public class JobordDetailItemDto
{
    [JsonPropertyName("prkey")]
    public long Prkey { get; set; }

    [JsonPropertyName("jobordno")]
    public string Jobordno { get; set; } = "";

    [JsonPropertyName("jobordsno")]
    public decimal Jobordsno { get; set; }

    [JsonPropertyName("prddt")]
    public string? Prddt { get; set; }

    [JsonPropertyName("delvedt")]
    public string? Delvedt { get; set; }

    [JsonPropertyName("shptm")]
    public string? Shptm { get; set; }

    [JsonPropertyName("itemcd")]
    public string? Itemcd { get; set; }

    [JsonPropertyName("cuscd")]
    public string? Cuscd { get; set; }

    [JsonPropertyName("shpctrcd")]
    public string? Shpctrcd { get; set; }

    [JsonPropertyName("cusitemcd")]
    public string? Cusitemcd { get; set; }

    [JsonPropertyName("jobordqun")]
    public decimal? Jobordqun { get; set; }

    [JsonPropertyName("linecd")]
    public string? Linecd { get; set; }

    [JsonPropertyName("jobordmernm")]
    public string? Jobordmernm { get; set; }

    [JsonPropertyName("item")]
    public ItemDetailDto? Item { get; set; }

    [JsonPropertyName("shpctr")]
    public ShpctrDetailDto? Shpctr { get; set; }

    [JsonPropertyName("routs")]
    public List<RoutDetailDto> Routs { get; set; } = new();

    [JsonPropertyName("mboms")]
    public List<MbomDetailDto> Mboms { get; set; } = new();

    [JsonPropertyName("cusmcd")]
    public CusmcdDetailDto? Cusmcd { get; set; }
}

public class SearchDetailResponseDto
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("items")]
    public List<JobordDetailItemDto> Items { get; set; } = new();
}

/// <summary>納品書検索結果1件（喫食日・納入場所名）</summary>
public class DeliveryNoteSearchResultDto
{
    [JsonPropertyName("eating_date")]
    public string? EatingDate { get; set; }

    [JsonPropertyName("location_name")]
    public string? LocationName { get; set; }

    [JsonPropertyName("location_code")]
    public string? LocationCode { get; set; }

    [JsonPropertyName("customer_code")]
    public string? CustomerCode { get; set; }
}

/// <summary>納品書検索APIレスポンス</summary>
public class DeliveryNoteSearchResponseDto
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("items")]
    public List<DeliveryNoteSearchResultDto> Items { get; set; } = new();
}

/// <summary>個人配送指示書検索結果1件（配送日・喫食時間・配送エリア）</summary>
public class PersonalDeliverySearchResultDto
{
    [JsonPropertyName("delivery_date")]
    public string? DeliveryDate { get; set; }

    [JsonPropertyName("time_name")]
    public string? TimeName { get; set; }

    [JsonPropertyName("area")]
    public string? Area { get; set; }
}

/// <summary>個人配送指示書検索APIレスポンス</summary>
public class PersonalDeliverySearchResponseDto
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("items")]
    public List<PersonalDeliverySearchResultDto> Items { get; set; } = new();
}

/// <summary>汁仕分表用：同一喫食日・喫食時間・品目の1納入場所分</summary>
public class JuiceSearchLocationDto
{
    [JsonPropertyName("shpctrnm")]
    public string? Shpctrnm { get; set; }

    [JsonPropertyName("jobordqun")]
    public decimal Jobordqun { get; set; }

    [JsonPropertyName("addinfo02")]
    public string? Addinfo02 { get; set; }
}

/// <summary>汁仕分表用：喫食日・喫食時間・品目でまとめた1グループ</summary>
public class JuiceSearchGroupDto
{
    [JsonPropertyName("delvedt")]
    public string? Delvedt { get; set; }

    [JsonPropertyName("shptm_display")]
    public string? ShptmDisplay { get; set; }

    [JsonPropertyName("itemcd")]
    public string? Itemcd { get; set; }

    [JsonPropertyName("jobordmernm")]
    public string? Jobordmernm { get; set; }

    [JsonPropertyName("locations")]
    public List<JuiceSearchLocationDto> Locations { get; set; } = new();
}

/// <summary>汁仕分表検索APIレスポンス（グループ単位）</summary>
public class JuiceSearchGroupResponseDto
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("groups")]
    public List<JuiceSearchGroupDto> Groups { get; set; } = new();
}

/// <summary>弁当箱盛り付け指示書（ご飯）用：同一喫食日・喫食時間・品目の1納入場所分</summary>
public class BentoSearchLocationDto
{
    [JsonPropertyName("shpctrnm")]
    public string? Shpctrnm { get; set; }

    [JsonPropertyName("jobordqun")]
    public decimal Jobordqun { get; set; }

    [JsonPropertyName("quantity")]
    public decimal Quantity { get; set; }

    [JsonPropertyName("addinfo02")]
    public string? Addinfo02 { get; set; }
}

/// <summary>弁当箱盛り付け指示書（ご飯）用：喫食日・喫食時間・品目でまとめた1グループ</summary>
public class BentoSearchGroupDto
{
    [JsonPropertyName("delvedt")]
    public string? Delvedt { get; set; }

    [JsonPropertyName("shptm_display")]
    public string? ShptmDisplay { get; set; }

    [JsonPropertyName("itemcd")]
    public string? Itemcd { get; set; }

    [JsonPropertyName("jobordmernm")]
    public string? Jobordmernm { get; set; }

    [JsonPropertyName("locations")]
    public List<BentoSearchLocationDto> Locations { get; set; } = new();
}

/// <summary>弁当箱盛り付け指示書（ご飯）検索APIレスポンス（グループ単位）</summary>
public class BentoSearchGroupResponseDto
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("groups")]
    public List<BentoSearchGroupDto> Groups { get; set; } = new();
}
