using BaggingInstructions.Api.Core;
using BaggingInstructions.Api.Entities;
using BaggingInstructions.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace BaggingInstructions.Api.Tests;

public class BaggingPrintedStateTests
{
    private static AppDbContext NewAppDb(string name)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"{name}-app")
            .Options;
        return new AppDbContext(options);
    }

    private static CstmeatDbContext NewOtherDb(string name)
    {
        var options = new DbContextOptionsBuilder<CstmeatDbContext>()
            .UseInMemoryDatabase($"{name}-other")
            .Options;
        return new CstmeatDbContext(options);
    }

    [Fact]
    public async Task MarkPrintedAsync_marks_baggedquantity_rows_for_product_and_parent_item()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var appDb = NewAppDb(dbName);
        await using var otherDb = NewOtherDb(dbName);
        var productDate = new DateOnly(2026, 5, 12);
        otherDb.BaggedQuantities.AddRange(
            new BaggedQuantity
            {
                BaggedQuantityId = 1,
                ProductDate = productDate,
                ParentItemCode = "P1",
                ChildItemCode = "C1",
                InputOrder = 1,
                IsPrinted = false,
                UpdatedAt = DateTime.UtcNow.AddDays(-1)
            },
            new BaggedQuantity
            {
                BaggedQuantityId = 2,
                ProductDate = productDate,
                ParentItemCode = "P1",
                ChildItemCode = "C2",
                InputOrder = 2,
                IsPrinted = false,
                UpdatedAt = DateTime.UtcNow.AddDays(-1)
            },
            new BaggedQuantity
            {
                BaggedQuantityId = 3,
                ProductDate = productDate,
                ParentItemCode = "P2",
                ChildItemCode = "C1",
                InputOrder = 1,
                IsPrinted = false,
                UpdatedAt = DateTime.UtcNow.AddDays(-1)
            });
        await otherDb.SaveChangesAsync();

        var service = new BaggingInputService(appDb, otherDb);
        await service.MarkPrintedAsync("20260512", "P1");

        var p1Rows = await otherDb.BaggedQuantities
            .Where(r => r.ParentItemCode == "P1")
            .OrderBy(r => r.InputOrder)
            .ToListAsync();
        Assert.All(p1Rows, r => Assert.True(r.IsPrinted));

        var p2Row = await otherDb.BaggedQuantities.SingleAsync(r => r.ParentItemCode == "P2");
        Assert.False(p2Row.IsPrinted);

        Assert.Empty(appDb.BaggingInputRegistrations);
    }

    [Fact]
    public async Task SearchBaggingGroupedAsync_reads_printed_state_from_baggedquantity()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var appDb = NewAppDb(dbName);
        await using var otherDb = NewOtherDb(dbName);
        var productDate = new DateOnly(2026, 5, 12);

        appDb.Units.Add(new Unit { UnitCode = "EA", UnitName = "Each" });
        appDb.Items.Add(new Item { ItemCd = "401001", ItemName = "Parent", UnitCode0 = "EA" });
        appDb.SalesOrderLines.Add(new SalesOrderLine
        {
            SalesOrderLineId = 100,
            ProductDate = productDate,
            ItemCd = "401001",
            Quantity = 10
        });
        appDb.BaggingInputRegistrations.Add(new BaggingInputRegistration
        {
            BaggingInputRegistrationId = 1,
            ProductDate = productDate,
            ItemCode = "401001",
            Payload = "{}",
            IsPrinted = false,
            UpdatedAt = DateTime.UtcNow
        });
        otherDb.BaggedQuantities.Add(new BaggedQuantity
        {
            BaggedQuantityId = 1,
            ProductDate = productDate,
            ParentItemCode = "401001",
            ChildItemCode = "C1",
            InputOrder = 1,
            IsPrinted = true,
            UpdatedAt = DateTime.UtcNow
        });
        await appDb.SaveChangesAsync();
        await otherDb.SaveChangesAsync();

        var service = new SearchService(appDb, otherDb);
        var groups = await service.SearchBaggingGroupedAsync("20260512", null);

        var group = Assert.Single(groups);
        Assert.True(group.IsPrinted);
    }

    [Fact]
    public async Task SearchBaggingGroupedAsync_returns_only_parent_items_starting_with_40()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var appDb = NewAppDb(dbName);
        await using var otherDb = NewOtherDb(dbName);
        var productDate = new DateOnly(2026, 5, 12);

        appDb.Items.AddRange(
            new Item { ItemCd = "401001", ItemName = "Bagging parent" },
            new Item { ItemCd = "501001", ItemName = "Non bagging parent" });
        appDb.SalesOrderLines.AddRange(
            new SalesOrderLine
            {
                SalesOrderLineId = 100,
                ProductDate = productDate,
                ItemCd = "401001",
                Quantity = 10
            },
            new SalesOrderLine
            {
                SalesOrderLineId = 101,
                ProductDate = productDate,
                ItemCd = "501001",
                Quantity = 20
            });
        await appDb.SaveChangesAsync();

        var service = new SearchService(appDb, otherDb);
        var groups = await service.SearchBaggingGroupedAsync("20260512", null);

        var group = Assert.Single(groups);
        Assert.Equal("401001", group.Itemcd);
        Assert.Equal(10, group.TotalJobordqun);
    }

    [Fact]
    public async Task GetRequiredQuantitiesAsync_defaults_spec_from_std_and_rounds_reference_quantity_up()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var appDb = NewAppDb(dbName);
        await using var otherDb = NewOtherDb(dbName);
        var productDate = new DateOnly(2026, 5, 12);
        var parent = new Item { ItemCd = "401001", ItemName = "Parent" };
        var child = new Item { ItemCd = "700001", ItemName = "Child" };
        var addInfo = new ItemAdditionalInformation
        {
            ItemCd = "401001",
            Std = "7.5",
            Car0 = 99m,
            Item = parent
        };
        parent.AdditionalInformation = addInfo;

        appDb.Items.AddRange(parent, child);
        appDb.ItemAdditionalInformations.Add(addInfo);
        var salesOrder = new SalesOrder { SalesOrderId = 1 };
        appDb.SalesOrders.Add(salesOrder);
        appDb.SalesOrderLines.Add(new SalesOrderLine
        {
            SalesOrderLineId = 100,
            SalesOrderId = salesOrder.SalesOrderId,
            SalesOrder = salesOrder,
            ProductDate = productDate,
            ItemCd = "401001",
            Item = parent,
            Quantity = 12.1m
        });
        appDb.Boms.Add(new Bom
        {
            BomId = 1,
            ParentItemCd = "401001",
            ChildItemCd = "700001",
            ChildItem = child,
            InputQty = 1m,
            OutputQty = 1m,
            ProductionOrder = 1m
        });
        await appDb.SaveChangesAsync();

        var search = new SearchService(appDb, otherDb);
        var calculator = new BaggingCalculatorService(
            search,
            new StockService(appDb),
            new BaggingInputService(appDb, otherDb));
        var result = await calculator.GetRequiredQuantitiesAsync(new[] { 100L });

        var line = Assert.Single(result.Lines);
        Assert.Equal(7.5m, line.SpecQty);
        Assert.Equal(13m, line.ReferenceQty);
    }
}
