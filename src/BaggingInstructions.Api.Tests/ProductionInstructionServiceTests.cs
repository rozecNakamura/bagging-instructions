using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BaggingInstructions.Api.Core;
using BaggingInstructions.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BaggingInstructions.Api.Tests;

public class ProductionInstructionServiceTests
{
    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task SearchAsync_InvalidDate_ThrowsArgumentException()
    {
        await using var db = CreateInMemoryDb();
        var service = new ProductionInstructionService(db);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SearchAsync("202404", null, null, null, CancellationToken.None));
    }
}

