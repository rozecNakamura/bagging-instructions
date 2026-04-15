using System.ComponentModel.DataAnnotations.Schema;

namespace BaggingInstructions.Api.Entities;

[Table("item")]
public class Item
{
    /// <summary>サロゲート ID（主キーは <see cref="ItemCd"/>）。craftlineax は bigint。</summary>
    [Column("itemid")]
    public long? ItemId { get; set; }

    [Column("itemcode")]
    public string ItemCd { get; set; } = "";

    [Column("itemname")]
    public string? ItemName { get; set; }

    [Column("shortname")]
    public string? ShortName { get; set; }

    [Column("activeflag")]
    public bool ActiveFlag { get; set; } = true;

    [Column("effectivefrom")]
    public DateOnly? EffectiveFrom { get; set; }

    [Column("effectiveto")]
    public DateOnly? EffectiveTo { get; set; }

    /// <summary>基準単位コード（unit.unitcode へ FK）。</summary>
    [Column("unitcode0")]
    public string? UnitCode0 { get; set; }

    [Column("unitcode1")]
    public string? UnitCode1 { get; set; }

    /// <summary>単位1→単位0 の換算（数量は qtyuni1×本値 で単位0）。</summary>
    [Column("conversionvalue1")]
    public decimal? ConversionValue1 { get; set; }

    /// <summary>単位2→単位0 の換算。</summary>
    [Column("conversionvalue2")]
    public decimal? ConversionValue2 { get; set; }

    /// <summary>単位3→単位0 の換算。</summary>
    [Column("conversionvalue3")]
    public decimal? ConversionValue3 { get; set; }

    [Column("unitcode2")]
    public string? UnitCode2 { get; set; }

    [Column("unitcode3")]
    public string? UnitCode3 { get; set; }

    [Column("shelflifedays")]
    public int? ShelflifeDays { get; set; }

    [Column("isstockmanaged")]
    public bool IsStockManaged { get; set; } = true;

    [Column("salesprice0")]
    public decimal? SalesPrice0 { get; set; }

    /// <summary>分類1コード。DB 列名は craftlineax の綴り classfication（i 欠け）。</summary>
    [Column("classfication1code")]
    public string? Classification1Code { get; set; }

    /// <summary>分類2コード（DB: classfication2code）。</summary>
    [Column("classfication2code")]
    public string? Classification2Code { get; set; }

    /// <summary>分類3コード（DB: classfication3code）。</summary>
    [Column("classfication3code")]
    public string? Classification3Code { get; set; }

    /// <summary>中分類コード（DB 列名は middleclassficationcode）。middleclassification.middleclassificationcode と対応。</summary>
    [Column("middleclassficationcode")]
    public string? MiddleClassificationCode { get; set; }

    public virtual Unit? Unit0 { get; set; }
    public virtual ItemAdditionalInformation? AdditionalInformation { get; set; }
    public virtual ICollection<ItemWorkCenterMapping> WorkCenterMappings { get; set; } = new List<ItemWorkCenterMapping>();
}
