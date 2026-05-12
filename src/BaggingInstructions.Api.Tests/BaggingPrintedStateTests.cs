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
        appDb.Items.Add(new Item { ItemCd = "P1", ItemName = "Parent", UnitCode0 = "EA" });
        appDb.SalesOrderLines.Add(new SalesOrderLine
        {
            SalesOrderLineId = 100,
            ProductDate = productDate,
            ItemCd = "P1",
            Quantity = 10
        });
        appDb.BaggingInputRegistrations.Add(new BaggingInputRegistration
        {
            BaggingInputRegistrationId = 1,
            ProductDate = productDate,
            ItemCode = "P1",
            Payload = "{}",
            IsPrinted = false,
            UpdatedAt = DateTime.UtcNow
        });
        otherDb.BaggedQuantities.Add(new BaggedQuantity
        {
            BaggedQuantityId = 1,
            ProductDate = productDate,
            ParentItemCode = "P1",
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
}
