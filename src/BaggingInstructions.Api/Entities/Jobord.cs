using System.ComponentModel.DataAnnotations.Schema;
using BaggingInstructions.Api.Entities.Legacy;

namespace BaggingInstructions.Api.Entities.Legacy;

[Table("jobord")]
public class Jobord
{
    /// <summary>サービスで Mbom を pfctcd,pdeptcd,pitemgr,pitemcd で取得してセットする。EF ではマッピングしない。</summary>
    [NotMapped]
    public ICollection<Mbom> Mboms { get; set; } = new List<Mbom>();

    [Column("prkey")]
    public long Prkey { get; set; }

    [Column("merfctcd")]
    public string Merfctcd { get; set; } = "";

    [Column("jobordno")]
    public string Jobordno { get; set; } = "";

    [Column("jobordsno")]
    public decimal Jobordsno { get; set; }

    [Column("shpsts")]
    public string? Shpsts { get; set; } = "";

    [Column("cusitemcd")]
    public string? Cusitemcd { get; set; } = "";

    [Column("fctcd")]
    public string? Fctcd { get; set; } = "";

    [Column("deptcd")]
    public string? Deptcd { get; set; } = "";

    [Column("itemgr")]
    public string? Itemgr { get; set; } = "";

    [Column("itemcd")]
    public string? Itemcd { get; set; } = "";

    [Column("joborddt")]
    public string? Joborddt { get; set; } = "";

    [Column("jobordqun")]
    public decimal Jobordqun { get; set; }

    [Column("joborduni")]
    public string? Joborduni { get; set; } = "";

    [Column("jobordcon")]
    public decimal Jobordcon { get; set; }

    [Column("jobordunip")]
    public decimal Jobordunip { get; set; }

    [Column("jobordvol")]
    public decimal Jobordvol { get; set; }

    [Column("jobordtax")]
    public decimal Jobordtax { get; set; }

    [Column("cuscd")]
    public string? Cuscd { get; set; } = "";

    [Column("shpctrcd")]
    public string? Shpctrcd { get; set; } = "";

    [Column("piccd")]
    public string? Piccd { get; set; } = "";

    [Column("jobordtyp")]
    public string? Jobordtyp { get; set; } = "";

    [Column("jobordjor")]
    public string? Jobordjor { get; set; } = "";

    [Column("stocprvqun")]
    public decimal Stocprvqun { get; set; }

    [Column("delvedt")]
    public string? Delvedt { get; set; } = "";

    [Column("schdshpdt")]
    public string? Schdshpdt { get; set; } = "";

    [Column("shpdt")]
    public string? Shpdt { get; set; } = "";

    [Column("shptm")]
    public string? Shptm { get; set; } = "";

    [Column("shpqun")]
    public decimal Shpqun { get; set; }

    [Column("kepshpqun")]
    public decimal Kepshpqun { get; set; }

    [Column("uuser")]
    public string? Uuser { get; set; } = "";

    [Column("udate")]
    public DateTime? Udate { get; set; }

    [Column("jobordmernm")]
    public string? Jobordmernm { get; set; }

    [Column("prddt")]
    public string? Prddt { get; set; }

    [Column("delvmemo")]
    public string? Delvmemo { get; set; } = "";

    [Column("cusitemordno")]
    public string? Cusitemordno { get; set; } = "";

    [Column("shopprc")]
    public decimal? Shopprc { get; set; }

    [Column("linecd")]
    public string? Linecd { get; set; } = "";

    [Column("delvcost")]
    public decimal Delvcost { get; set; }

    [Column("delvothercost")]
    public decimal Delvothercost { get; set; }

    // その他カラム（省略時はデフォルト値でマッピング）
    [Column("sasrecdt")]
    public string? Sasrecdt { get; set; } = "";

    [Column("sasjobordlstno")]
    public string? Sasjobordlstno { get; set; } = "";

    [Column("seltyp")]
    public string? Seltyp { get; set; } = "";

    [Column("shpdelvdt")]
    public string? Shpdelvdt { get; set; } = "";

    [Column("jobordoptinfo1")]
    public string? Jobordoptinfo1 { get; set; } = "";

    [Column("jobordoptinfo2")]
    public string? Jobordoptinfo2 { get; set; } = "";

    [Column("jobordoptinfo3")]
    public string? Jobordoptinfo3 { get; set; } = "";

    [Column("jobordoptinfo4")]
    public string? Jobordoptinfo4 { get; set; } = "";

    [Column("shpblocksts")]
    public string? Shpblocksts { get; set; } = "";

    [Column("shpblockqun")]
    public decimal Shpblockqun { get; set; }

    [Column("hiddenjor")]
    public string? Hiddenjor { get; set; } = "";

    [Column("jobordregtyp")]
    public string? Jobordregtyp { get; set; } = "";

    [Column("plandelvcd")]
    public string? Plandelvcd { get; set; } = "";

    [Column("jobordkeptyp")]
    public string? Jobordkeptyp { get; set; } = "";

    [Column("shpctritemcd")]
    public string? Shpctritemcd { get; set; } = "";

    [Column("jobordgrp1")]
    public string? Jobordgrp1 { get; set; } = "";

    [Column("trcd")]
    public string? Trcd { get; set; } = "";

    [Column("jobordtaxinc")]
    public decimal Jobordtaxinc { get; set; }

    [Column("orgplacecd")]
    public string? Orgplacecd { get; set; } = "";

    [Column("gradecd")]
    public string? Gradecd { get; set; } = "";

    [Column("organic")]
    public string? Organic { get; set; } = "";

    [Column("carqun")]
    public decimal Carqun { get; set; }

    [Column("casequn")]
    public decimal Casequn { get; set; }

    [Column("jobordwei")]
    public decimal Jobordwei { get; set; }

    [Column("shplock")]
    public string? Shplock { get; set; } = "";

    [Column("confshpqun")]
    public decimal Confshpqun { get; set; }

    [Column("csvplandelvcd")]
    public string? Csvplandelvcd { get; set; } = "";

    [Column("nagcsslipno")]
    public string? Nagcsslipno { get; set; } = "";

    [Column("nagcsslipsno")]
    public decimal Nagcsslipsno { get; set; }

    [Column("fullmeasure")]
    public string? Fullmeasure { get; set; } = "";

    [Column("jobordmerkanna")]
    public string? Jobordmerkanna { get; set; } = "";

    [Column("csvuninm")]
    public string? Csvuninm { get; set; } = "";

    [Column("editflg")]
    public string? Editflg { get; set; } = "";

    [Column("prtsts")]
    public string? Prtsts { get; set; } = "0";

    [Column("contjobordno")]
    public string? Contjobordno { get; set; } = "";

    [Column("delvememo")]
    public string? Delvememo { get; set; } = "";

    [Column("delvtzcd")]
    public string? Delvtzcd { get; set; } = "";

    [Column("bildt")]
    public string? Bildt { get; set; } = "";

    [Column("cusindcd")]
    public string? Cusindcd { get; set; } = "";

    [Column("jobordtaxrate")]
    public decimal Jobordtaxrate { get; set; }

    [Column("pendshpdtflg")]
    public string? Pendshpdtflg { get; set; } = "";

    [Column("dataerrflg01")]
    public string? Dataerrflg01 { get; set; } = "0";

    [Column("dataerrflg02")]
    public string? Dataerrflg02 { get; set; } = "0";

    [Column("dataerrflg03")]
    public string? Dataerrflg03 { get; set; } = "0";

    [Column("dataerrflg04")]
    public string? Dataerrflg04 { get; set; } = "0";

    [Column("genprtflg01")]
    public string? Genprtflg01 { get; set; } = "0";

    [Column("genprtflg02")]
    public string? Genprtflg02 { get; set; } = "0";

    [Column("genprtflg03")]
    public string? Genprtflg03 { get; set; } = "0";

    [Column("genprtflg04")]
    public string? Genprtflg04 { get; set; } = "0";

    [Column("genprtflg05")]
    public string? Genprtflg05 { get; set; } = "0";

    // ナビゲーション
    public virtual ItemLegacy? Item { get; set; }
    public virtual Shpctr? Shpctr { get; set; }
    public virtual Cusmcd? Cusmcd { get; set; }
}
