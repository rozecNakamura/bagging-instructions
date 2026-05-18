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
    public DbSet<Mshokushu> Mshokushus => Set<Mshokushu>();
    public DbSet<MShisetsu> MShisetsus => Set<MShisetsu>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Foodtype>().HasKey(f => f.Foodtypeid);
        modelBuilder.Entity<Eattime>().HasKey(e => e.Eattimecd);

        var dateOnlyConverter = new ValueConverter<DateOnly, DateTime>(
            v => new DateTime(v.Year, v.Month, v.Day, 0, 0, 0, DateTimeKind.Unspecified),
            v => DateOnly.FromDateTime(v));

        modelBuilder.Entity<BaggedQuantity>()
            .HasKey(e => e.BaggedQuantityId);
        // baggedquantityid はアプリ側で採番（テーブルに GENERATED … AS IDENTITY / nextval 既定が無い DDL でも保存可能）
        modelBuilder.Entity<BaggedQuantity>()
            .Property(e => e.BaggedQuantityId)
            .ValueGeneratedNever();
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
            .Property(e => e.IsPrinted)
            .HasDefaultValue(false);
        modelBuilder.Entity<BaggedQuantity>()
            .Property(e => e.IsInstructionPrinted)
            .HasDefaultValue(false);
        modelBuilder.Entity<BaggedQuantity>()
            .Property(e => e.IsLabelPrinted)
            .HasDefaultValue(false);
        modelBuilder.Entity<BaggedQuantity>()
            .HasIndex(e => new { e.ProductDate, e.ParentItemCode });

        modelBuilder.Entity<Mshokushu>().HasKey(m => m.Id);
        modelBuilder.Entity<MShisetsu>().HasKey(s => s.Id);
    }
}
