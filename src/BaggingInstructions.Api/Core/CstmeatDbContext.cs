using Microsoft.EntityFrameworkCore;
using BaggingInstructions.Api.Entities;

namespace BaggingInstructions.Api.Core;

/// <summary>craftlineaxother データベース用 DbContext（cstmeat, foodtype, eattime）</summary>
public class CstmeatDbContext : DbContext
{
    public CstmeatDbContext(DbContextOptions<CstmeatDbContext> options) : base(options) { }

    public DbSet<Cstmeat> Cstmeats => Set<Cstmeat>();
    public DbSet<Foodtype> Foodtypes => Set<Foodtype>();
    public DbSet<Eattime> Eattimes => Set<Eattime>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Foodtype>().HasKey(f => f.Foodtypeid);
        modelBuilder.Entity<Eattime>().HasKey(e => e.Eattimecd);
    }
}
