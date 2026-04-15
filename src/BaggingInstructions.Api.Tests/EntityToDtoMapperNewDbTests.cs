using BaggingInstructions.Api.Entities;
using BaggingInstructions.Api.Services;
using Xunit;

namespace BaggingInstructions.Api.Tests;

public class EntityToDtoMapperNewDbTests
{
    [Fact]
    public void ToMbomDetailDto_maps_child_item_additional_information_car_fields()
    {
        var child = new Item
        {
            ItemCd = "CHILD01",
            ItemName = "Child name",
            Unit0 = new Unit { UnitCode = "U", UnitName = "個" },
            AdditionalInformation = new ItemAdditionalInformation
            {
                ItemCd = "CHILD01",
                Car1 = 10m,
                Car2 = 20m,
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
        Assert.Equal(10m, dto.ChildItem!.Car1);
        Assert.Equal(20m, dto.ChildItem.Car2);
        Assert.Null(dto.ChildItem.Car3);
        Assert.Equal(5m, dto.ChildItem.Kikunip);
    }
}
