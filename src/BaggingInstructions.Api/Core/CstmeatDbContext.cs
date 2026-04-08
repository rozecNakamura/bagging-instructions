using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using BaggingInstructions.Api.Entities;

namespace BaggingInstructions.Api.Core;

/// <summary>craftlineaxother データベース用 DbContext（cstmeat, foodtype, eattime, baggedquantity）</summary>
public class CstmeatDbContext : DbContext
{
    public CstmeatDbContext(DbContextOptions<CstmeatDbContext> options) : base(options) { }

    public DbSet<Cstmeat> Cstmeats => Set<Cstmeat>();
    public DbSet<Foodtype> Foodtypes => Set<Foodtype>();
    public DbSet<Eattime> Eattimes => Set<Eattime>();
    public DbSet<BaggedQuantity> BaggedQuantities => Set<BaggedQuantity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Foodtype>().HasKey(f => f.Foodtypeid);
        modelBuilder.Entity<Eattime>().HasKey(e => e.Eattimecd);

        var dateOnlyConverter = new ValueConverter<DateOnly, DateTime>(
            v => new DateTime(v.Year, v.Month, v.Day, 0, 0, 0, DateTimeKind.Unspecified),
            v => DateOnly.FromDateTime(v));

        modelBuilder.Entity<BaggedQuantity>()
            .HasKey(e => e.BaggedQuantityId);
        modelBuilder.Entity<BaggedQuantity>()
            .Property(e => e.BaggedQuantityId)
            .UseIdentityByDefaultColumn();
        modelBuilder.Entity<BaggedQuantity>()
            .Property(e => e.ParentItemCode)
            .HasMaxLength(128);
        modelBuilder.Entity<BaggedQuantity>()
            .Property(e => e.ChildItemCode)
            .HasMaxLength(128);
        modelBuilder.Entity<BaggedQuantity>()
            .Property(e => e.ProductDate)
            .HasConversion(dateOnlyConverter);
        modelBuilder.Entity<BaggedQuantity>()
            .HasIndex(e => new { e.ProductDate, e.ParentItemCode });
    }
}
