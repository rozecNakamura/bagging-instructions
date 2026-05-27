using System.ComponentModel.DataAnnotations.Schema;

namespace BaggingInstructions.Api.Entities;

/// <summary>craftlineaxother データベースの item_definition_master テーブル（単価コード名マスタ）</summary>
[Table("item_definition_master")]
public class ItemDefinitionMaster
{
    [Column("item_code")]
    public string? ItemCode { get; set; }

    [Column("item_def")]
    public string? ItemDef { get; set; }

    [Column("item_name")]
    public string? ItemName { get; set; }
}
