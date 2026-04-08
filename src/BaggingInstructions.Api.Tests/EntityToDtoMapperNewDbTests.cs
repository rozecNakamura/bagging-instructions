using BaggingInstructions.Api.Entities;
using BaggingInstructions.Api.Services;
using Xunit;

namespace BaggingInstructions.Api.Tests;

public class EntityToDtoMapperNewDbTests
{
    [Fact]
    public void ToMbomDetailDto_maps_child_item_additional_information_std_fields()
    {
        var child = new Item
        {
            ItemCd = "CHILD01",
            ItemName = "Child name",
            Unit0 = new Unit { UnitCode = "U", UnitName = "個" },
            AdditionalInformation = new ItemAdditionalInformation
            {
                ItemCd = "CHILD01",
                Std1 = "10",
                Std2 = "20",
                Car0 = 5m
            }
        };
        var bom = new Bom
        {
            BomId = 1,
            ParentItemCd = "PARENT",
            ChildItemCd = "CHILD01",
            InputQty = 2,
            OutputQty = 1
        };

        var dto = EntityToDtoMapper.ToMbomDetailDto(bom, child, child.Unit0);

        Assert.NotNull(dto.ChildItem);
        Assert.Equal("10", dto.ChildItem!.Std1);
        Assert.Equal("20", dto.ChildItem.Std2);
        Assert.Null(dto.ChildItem.Std3);
        Assert.Equal(5m, dto.ChildItem.Kikunip);
    }
}
