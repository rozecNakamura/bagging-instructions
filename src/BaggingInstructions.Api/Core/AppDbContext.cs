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
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<ReportOutputSetting> ReportOutputSettings => Set<ReportOutputSetting>();
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
    public DbSet<MiddleClassification> MiddleClassifications => Set<MiddleClassification>();
    public DbSet<BaggingInputRegistration> BaggingInputRegistrations => Set<BaggingInputRegistration>();

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

        // Unit（主キー unitcode）
        modelBuilder.Entity<Unit>().HasKey(u => u.UnitCode);
        modelBuilder.Entity<Unit>().Property(u => u.UnitCode).HasMaxLength(64);

        // Item（主キー itemcode）
        modelBuilder.Entity<Item>().HasKey(i => i.ItemCd);
        modelBuilder.Entity<Item>().Property(i => i.ItemCd).HasMaxLength(128);

        // ItemAdditionalInformation（主キー itemcode）
        modelBuilder.Entity<ItemAdditionalInformation>().HasKey(ai => ai.ItemCd);
        modelBuilder.Entity<ItemAdditionalInformation>().Property(ai => ai.ItemCd).HasMaxLength(128);

        // Workcenter（主キー workcentercode）
        modelBuilder.Entity<Workcenter>().HasKey(w => w.WorkcenterCode);
        modelBuilder.Entity<Workcenter>().Property(w => w.WorkcenterCode).HasMaxLength(64);

        modelBuilder.Entity<SalesOrderLineAddinfo>().HasKey(a => a.SalesOrderLineId);

        // SalesOrder -> Customer（customercode）, CustomerDeliveryLocation（customercode + locationcode）
        modelBuilder.Entity<SalesOrder>()
            .HasKey(so => so.SalesOrderId);
        modelBuilder.Entity<SalesOrderLine>()
            .HasKey(l => l.SalesOrderLineId);

        modelBuilder.Entity<SalesOrder>()
            .HasOne(so => so.Customer)
            .WithMany()
            .HasForeignKey(so => so.CustomerCode)
            .HasPrincipalKey(c => c.CustomerCode)
            .IsRequired(false);
        modelBuilder.Entity<SalesOrder>()
            .HasOne(so => so.CustomerDeliveryLocation)
            .WithMany()
            .HasForeignKey(so => new { so.CustomerCode, so.CustomerDeliveryLocationCode })
            .HasPrincipalKey(cdl => new { cdl.CustomerCode, cdl.LocationCode })
            .IsRequired(false);

        // SalesOrderLine -> SalesOrder, Item, CustomerItem, SalesOrderLineAddinfo
        modelBuilder.Entity<SalesOrderLine>()
            .HasOne(l => l.SalesOrder)
            .WithMany(so => so.SalesOrderLines)
            .HasForeignKey(l => l.SalesOrderId);
        modelBuilder.Entity<SalesOrderLine>()
            .HasOne(l => l.Item)
            .WithMany()
            .HasForeignKey(l => l.ItemCd)
            .HasPrincipalKey(i => i.ItemCd)
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

        // Item -> Unit（unitcode0 → unit.unitcode）
        modelBuilder.Entity<Item>()
            .HasOne(i => i.Unit0)
            .WithMany()
            .HasForeignKey(i => i.UnitCode0)
            .HasPrincipalKey(u => u.UnitCode)
            .IsRequired(false);
        modelBuilder.Entity<Item>()
            .HasOne(i => i.AdditionalInformation)
            .WithOne(ai => ai.Item)
            .HasForeignKey<ItemAdditionalInformation>(ai => ai.ItemCd)
            .HasPrincipalKey<Item>(i => i.ItemCd)
            .IsRequired(false);

        // Item -> ItemWorkCenterMapping (FK: item.itemcode = mapping.itemcode; property ItemCd)
        modelBuilder.Entity<Item>()
            .HasMany(i => i.WorkCenterMappings)
            .WithOne()
            .HasForeignKey(m => m.ItemCd)
            .HasPrincipalKey(i => i.ItemCd)
            .IsRequired(false);
        modelBuilder.Entity<Workcenter>()
            .HasIndex(w => w.WorkcenterCode)
            .IsUnique();

        modelBuilder.Entity<ItemWorkCenterMapping>()
            .HasKey(m => new { m.ItemCd, m.WorkcenterCode });
        modelBuilder.Entity<ItemWorkCenterMapping>()
            .HasOne(m => m.Workcenter)
            .WithMany()
            .HasForeignKey(m => m.WorkcenterCode)
            .HasPrincipalKey(w => w.WorkcenterCode);

        // Bom -> ChildItem (ChildItemCd -> Item.ItemCd)
        modelBuilder.Entity<Bom>()
            .HasOne(b => b.ChildItem)
            .WithMany()
            .HasForeignKey(b => b.ChildItemCd)
            .HasPrincipalKey(i => i.ItemCd)
            .IsRequired(false);

        // Stock -> Item, Warehouse（FK: stock.itemcode = item.itemcode）
        modelBuilder.Entity<Stock>()
            .HasOne(s => s.Item)
            .WithMany()
            .HasForeignKey(s => s.ItemCd)
            .HasPrincipalKey(i => i.ItemCd)
            .IsRequired(false);
        modelBuilder.Entity<Stock>()
            .HasOne(s => s.Warehouse)
            .WithMany()
            .HasForeignKey(s => s.WarehouseId);

        // Customer（craftlineax: 本プロジェクトでは customercode を主キーとしてマッピング）
        modelBuilder.Entity<Customer>()
            .HasKey(c => c.CustomerCode);
        modelBuilder.Entity<Customer>()
            .Property(c => c.CustomerCode)
            .HasMaxLength(64);

        modelBuilder.Entity<CustomerDeliveryLocation>()
            .HasKey(c => c.DeliveryLocationId);
        // CustomerDeliveryLocation -> Customer（customerdeliverylocation.customercode = customer.customercode）
        modelBuilder.Entity<CustomerDeliveryLocation>()
            .HasOne(l => l.Customer)
            .WithMany(c => c.DeliveryLocations)
            .HasForeignKey(l => l.CustomerCode)
            .HasPrincipalKey(c => c.CustomerCode);
        // CustomerDeliveryLocation -> CustomerDeliveryLocationAddinfo（FK は customercode + deliverylocationcode）
        modelBuilder.Entity<CustomerDeliveryLocationAddinfo>()
            .HasKey(a => a.AddinfoId);
        modelBuilder.Entity<CustomerDeliveryLocation>()
            .HasOne(c => c.Addinfo)
            .WithOne(a => a.CustomerDeliveryLocation)
            .HasForeignKey<CustomerDeliveryLocationAddinfo>(
                nameof(CustomerDeliveryLocationAddinfo.CustomerCode),
                nameof(CustomerDeliveryLocationAddinfo.LocationCode))
            .HasPrincipalKey<CustomerDeliveryLocation>(
                nameof(CustomerDeliveryLocation.CustomerCode),
                nameof(CustomerDeliveryLocation.LocationCode))
            .IsRequired(false);

        // CustomerItem -> Customer, Item（customer は customercode で結合）
        modelBuilder.Entity<CustomerItem>()
            .HasOne(ci => ci.Customer)
            .WithMany()
            .HasForeignKey(ci => ci.CustomerCode)
            .HasPrincipalKey(c => c.CustomerCode)
            .IsRequired(false);
        modelBuilder.Entity<CustomerItem>()
            .HasOne(ci => ci.Item)
            .WithMany()
            .HasForeignKey(ci => ci.ItemCd)
            .HasPrincipalKey(i => i.ItemCd)
            .IsRequired(false);

        modelBuilder.Entity<MajorClassification>()
            .HasKey(m => m.MajorClassificationId);

        modelBuilder.Entity<MiddleClassification>()
            .HasKey(m => m.MiddleClassificationId);

        modelBuilder.Entity<Supplier>()
            .HasKey(s => s.SupplierCode);

        modelBuilder.Entity<ReportOutputSetting>()
            .HasKey(r => r.ReportCode);

        modelBuilder.Entity<BaggingInputRegistration>()
            .HasKey(r => r.BaggingInputRegistrationId);
        modelBuilder.Entity<BaggingInputRegistration>()
            .Property(r => r.BaggingInputRegistrationId)
            .UseIdentityByDefaultColumn();
        modelBuilder.Entity<BaggingInputRegistration>()
            .Property(r => r.ItemCode)
            .HasMaxLength(128);
        modelBuilder.Entity<BaggingInputRegistration>()
            .Property(r => r.Payload)
            .HasColumnType("text");
        modelBuilder.Entity<BaggingInputRegistration>()
            .Property(r => r.ProductDate)
            .HasConversion(
                v => new DateTime(v.Year, v.Month, v.Day, 0, 0, 0, DateTimeKind.Unspecified),
                v => DateOnly.FromDateTime(v));
        modelBuilder.Entity<BaggingInputRegistration>()
            .HasIndex(r => new { r.ProductDate, r.ItemCode })
            .IsUnique();
    }
}
