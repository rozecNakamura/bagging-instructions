using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using BaggingInstructions.Api.Entities;

namespace BaggingInstructions.Api.Core;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // 新DB（craftlineax）エンティティ
    public DbSet<SalesOrder> SalesOrders => Set<SalesOrder>();
    public DbSet<SalesOrderLine> SalesOrderLines => Set<SalesOrderLine>();
    public DbSet<SalesOrderLineAddinfo> SalesOrderLineAddinfos => Set<SalesOrderLineAddinfo>();
    public DbSet<OrderTable> OrderTables => Set<OrderTable>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<ItemAdditionalInformation> ItemAdditionalInformations => Set<ItemAdditionalInformation>();
    public DbSet<Bom> Boms => Set<Bom>();
    public DbSet<Stock> Stocks => Set<Stock>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<Workcenter> Workcenters => Set<Workcenter>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerDeliveryLocation> CustomerDeliveryLocations => Set<CustomerDeliveryLocation>();
    public DbSet<CustomerDeliveryLocationAddinfo> CustomerDeliveryLocationAddinfos => Set<CustomerDeliveryLocationAddinfo>();
    public DbSet<CustomerItem> CustomerItems => Set<CustomerItem>();
    public DbSet<ItemWorkCenterMapping> ItemWorkCenterMappings => Set<ItemWorkCenterMapping>();
    public DbSet<MajorClassification> MajorClassifications => Set<MajorClassification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // DB が timestamp without time zone の列を DateOnly で読めるように変換
        var dateOnlyNullableConverter = new ValueConverter<DateOnly?, DateTime?>(
            v => v.HasValue ? new DateTime(v.Value.Year, v.Value.Month, v.Value.Day, 0, 0, 0, DateTimeKind.Unspecified) : null,
            v => v.HasValue ? DateOnly.FromDateTime(v.Value) : null);
        var dateOnlyConverter = new ValueConverter<DateOnly, DateTime>(
            v => new DateTime(v.Year, v.Month, v.Day, 0, 0, 0, DateTimeKind.Unspecified),
            v => DateOnly.FromDateTime(v));
        modelBuilder.Entity<SalesOrderLine>().Property(e => e.PlannedShipDate).HasConversion(dateOnlyNullableConverter);
        modelBuilder.Entity<SalesOrderLine>().Property(e => e.PlannedDeliveryDate).HasConversion(dateOnlyNullableConverter);
        modelBuilder.Entity<SalesOrderLine>().Property(e => e.ProductDate).HasConversion(dateOnlyNullableConverter);
        modelBuilder.Entity<SalesOrder>().Property(e => e.OrderDate).HasConversion(dateOnlyNullableConverter);
        modelBuilder.Entity<Item>().Property(e => e.EffectiveFrom).HasConversion(dateOnlyNullableConverter);
        modelBuilder.Entity<Item>().Property(e => e.EffectiveTo).HasConversion(dateOnlyNullableConverter);
        modelBuilder.Entity<Bom>().Property(e => e.StartDate).HasConversion(dateOnlyNullableConverter);
        modelBuilder.Entity<Bom>().Property(e => e.EndDate).HasConversion(dateOnlyNullableConverter);

        // Unit
        modelBuilder.Entity<Unit>().HasIndex(u => u.UnitCode).IsUnique();

        // SalesOrder -> Customer, CustomerDeliveryLocation
        modelBuilder.Entity<SalesOrder>()
            .HasOne(so => so.Customer)
            .WithMany()
            .HasForeignKey(so => so.CustomerId)
            .IsRequired(false);
        modelBuilder.Entity<SalesOrder>()
            .HasOne(so => so.CustomerDeliveryLocation)
            .WithMany()
            .HasForeignKey(so => so.CustomerDeliveryLocationId)
            .HasPrincipalKey(c => c.DeliveryLocationId)
            .IsRequired(false);

        // SalesOrderLine -> SalesOrder, Item, CustomerItem, SalesOrderLineAddinfo
        modelBuilder.Entity<SalesOrderLine>()
            .HasOne(l => l.SalesOrder)
            .WithMany(so => so.SalesOrderLines)
            .HasForeignKey(l => l.SalesOrderId);
        modelBuilder.Entity<SalesOrderLine>()
            .HasOne(l => l.Item)
            .WithMany()
            .HasForeignKey(l => l.ItemId)
            .IsRequired(false);
        modelBuilder.Entity<SalesOrderLine>()
            .HasOne(l => l.CustomerItem)
            .WithMany()
            .HasForeignKey(l => l.CustomerItemId)
            .IsRequired(false);
        modelBuilder.Entity<SalesOrderLine>()
            .HasOne(l => l.Addinfo)
            .WithOne(a => a.SalesOrderLine)
            .HasForeignKey<SalesOrderLineAddinfo>(a => a.SalesOrderLineId)
            .IsRequired(false);
        modelBuilder.Entity<SalesOrderLine>()
            .HasOne(l => l.OrderTable)
            .WithOne(o => o.SalesOrderLine)
            .HasForeignKey<OrderTable>(o => o.SalesOrderLineId)
            .IsRequired(false);
        modelBuilder.Entity<OrderTable>()
            .HasKey(o => o.SalesOrderLineId);

        // Item -> Unit, ItemAdditionalInformation
        modelBuilder.Entity<Item>()
            .HasOne(i => i.Unit0)
            .WithMany()
            .HasForeignKey(i => i.UnitId0)
            .IsRequired(false);
        modelBuilder.Entity<Item>()
            .HasOne(i => i.AdditionalInformation)
            .WithOne(ai => ai.Item)
            .HasForeignKey<ItemAdditionalInformation>(ai => ai.ItemId);

        // Item -> ItemWorkCenterMapping (ItemCd)
        modelBuilder.Entity<Item>()
            .HasMany(i => i.WorkCenterMappings)
            .WithOne()
            .HasForeignKey(m => m.ItemCd)
            .HasPrincipalKey(i => i.ItemCd)
            .IsRequired(false);
        modelBuilder.Entity<ItemWorkCenterMapping>()
            .HasKey(m => new { m.ItemCd, m.WorkcenterId });
        modelBuilder.Entity<ItemWorkCenterMapping>()
            .HasOne(m => m.Workcenter)
            .WithMany()
            .HasForeignKey(m => m.WorkcenterId);

        // Bom -> ChildItem (ChildItemCd -> Item.ItemCd)
        modelBuilder.Entity<Bom>()
            .HasOne(b => b.ChildItem)
            .WithMany()
            .HasForeignKey(b => b.ChildItemCd)
            .HasPrincipalKey(i => i.ItemCd)
            .IsRequired(false);

        // Stock -> Item, Warehouse
        modelBuilder.Entity<Stock>()
            .HasOne(s => s.Item)
            .WithMany()
            .HasForeignKey(s => s.ItemId);
        modelBuilder.Entity<Stock>()
            .HasOne(s => s.Warehouse)
            .WithMany()
            .HasForeignKey(s => s.WarehouseId);

        // CustomerDeliveryLocation: 主キー明示（EF が DeliveryLocationId を自動認識しないため）
        modelBuilder.Entity<CustomerDeliveryLocation>()
            .HasKey(c => c.DeliveryLocationId);
        // CustomerDeliveryLocation -> Customer
        modelBuilder.Entity<CustomerDeliveryLocation>()
            .HasOne(c => c.Customer)
            .WithMany(c => c.DeliveryLocations)
            .HasForeignKey(c => c.CustomerId);
        // CustomerDeliveryLocation -> CustomerDeliveryLocationAddinfo (1:1)
        modelBuilder.Entity<CustomerDeliveryLocation>()
            .HasOne(c => c.Addinfo)
            .WithOne(a => a.CustomerDeliveryLocation)
            .HasForeignKey<CustomerDeliveryLocationAddinfo>(a => a.DeliveryLocationId)
            .IsRequired(false);

        // CustomerItem -> Customer, Item
        modelBuilder.Entity<CustomerItem>()
            .HasOne(ci => ci.Customer)
            .WithMany()
            .HasForeignKey(ci => ci.CustomerId)
            .IsRequired(false);
        modelBuilder.Entity<CustomerItem>()
            .HasOne(ci => ci.Item)
            .WithMany()
            .HasForeignKey(ci => ci.ItemId)
            .IsRequired(false);

        modelBuilder.Entity<MajorClassification>()
            .HasKey(m => m.MajorClassificationId);
    }
}
