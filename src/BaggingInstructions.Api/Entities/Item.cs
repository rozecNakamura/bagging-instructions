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

    /// <summary>大分類コード（DB: majorclassificationcode）。majorclassification.majorclassificationcode と対応。</summary>
    [Column("majorclassificationcode")]
    public string? MajorClassificationCode { get; set; }

    /// <summary>分類1コード（DB: classification1code）。</summary>
    [Column("classification1code")]
    public string? Classification1Code { get; set; }

    /// <summary>分類2コード（DB: classification2code）。</summary>
    [Column("classification2code")]
    public string? Classification2Code { get; set; }

    /// <summary>分類3コード（DB: classification3code）。</summary>
    [Column("classification3code")]
    public string? Classification3Code { get; set; }

    /// <summary>仕入先コード（計量器連携 ITEM.csv の LOCCD 候補）。</summary>
    [Column("suppliercode")]
    public string? SupplierCode { get; set; }

    /// <summary>作業区コード（計量器連携 ITEM.csv の LOCCD 候補）。</summary>
    [Column("workcentercode")]
    public string? WorkCenterCode { get; set; }

    /// <summary>倉庫コード（計量器連携 ITEM.csv の WHCD）。</summary>
    [Column("warehousecode")]
    public string? WarehouseCode { get; set; }

    /// <summary>中分類コード（DB: middleclassificationcode）。middleclassification.middleclassificationcode と対応。</summary>
    [Column("middleclassificationcode")]
    public string? MiddleClassificationCode { get; set; }

    /// <summary>小分類コード（DB: minorclassificationcode）。minorclassification.minorclassificationcode と対応。</summary>
    [Column("minorclassificationcode")]
    public string? MinorClassificationCode { get; set; }

    public virtual Unit? Unit0 { get; set; }
    public virtual ItemAdditionalInformation? AdditionalInformation { get; set; }
    public virtual ICollection<ItemWorkCenterMapping> WorkCenterMappings { get; set; } = new List<ItemWorkCenterMapping>();
    public virtual Classification1? Classification1 { get; set; }
}
