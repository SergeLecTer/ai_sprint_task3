using B2BShop.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace B2BShop.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Article> Articles => Set<Article>();
    public DbSet<ArticleVariant> ArticleVariants => Set<ArticleVariant>();
    public DbSet<ArticleStockLevel> StockLevels => Set<ArticleStockLevel>();
    public DbSet<ProductCategory> Categories => Set<ProductCategory>();
    public DbSet<ProductGroup> ProductGroups => Set<ProductGroup>();
    public DbSet<ArticleProductGroupMembership> ProductGroupMemberships => Set<ArticleProductGroupMembership>();
    public DbSet<CompanyPriceList> PriceLists => Set<CompanyPriceList>();
    public DbSet<PriceListLine> PriceListLines => Set<PriceListLine>();
    public DbSet<VolumePriceTier> VolumePriceTiers => Set<VolumePriceTier>();
    public DbSet<ContractPrice> ContractPrices => Set<ContractPrice>();
    public DbSet<PromotionalDiscount> PromotionalDiscounts => Set<PromotionalDiscount>();
    public DbSet<TaxRule> TaxRules => Set<TaxRule>();
    public DbSet<CurrencyRate> CurrencyRates => Set<CurrencyRate>();
    public DbSet<CompanyUiConfiguration> UiConfigurations => Set<CompanyUiConfiguration>();
    public DbSet<CompanyCheckoutConfig> CheckoutConfigs => Set<CompanyCheckoutConfig>();
    public DbSet<CheckoutStepConfig> CheckoutSteps => Set<CheckoutStepConfig>();
    public DbSet<CompanyProductAssignment> ProductAssignments => Set<CompanyProductAssignment>();
    public DbSet<CompanyCategoryAssignment> CategoryAssignments => Set<CompanyCategoryAssignment>();
    public DbSet<IntegrationConfig> IntegrationConfigs => Set<IntegrationConfig>();
    public DbSet<DeliveryAddress> DeliveryAddresses => Set<DeliveryAddress>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();
    public DbSet<OrderTransferLog> TransferLogs => Set<OrderTransferLog>();
    public DbSet<ShoppingList> ShoppingLists => Set<ShoppingList>();
    public DbSet<ShoppingListLine> ShoppingListLines => Set<ShoppingListLine>();
    public DbSet<Quote> Quotes => Set<Quote>();
    public DbSet<QuoteLine> QuoteLines => Set<QuoteLine>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        base.OnModelCreating(mb);

        // Company
        mb.Entity<Company>(b => {
            b.HasKey(e => e.Id);
            b.HasIndex(e => e.Code).IsUnique();
            b.Property(e => e.Code).HasMaxLength(50).IsRequired();
            b.Property(e => e.Name).HasMaxLength(200).IsRequired();
            b.HasOne(e => e.UiConfig).WithOne(u => u.Company).HasForeignKey<CompanyUiConfiguration>(u => u.CompanyId);
            b.HasOne(e => e.CheckoutConfig).WithOne(c => c.Company).HasForeignKey<CompanyCheckoutConfig>(c => c.CompanyId);
        });

        // User
        mb.Entity<User>(b => {
            b.HasKey(e => e.Id);
            b.HasIndex(e => e.Email).IsUnique();
            b.Property(e => e.Email).HasMaxLength(255).IsRequired();
        });

        // Article — "Product" in other places, "Item" in legacy pricing
        mb.Entity<Article>(b => {
            b.HasKey(e => e.Id);
            b.HasIndex(e => e.Code).IsUnique();
            b.Property(e => e.Code).HasMaxLength(100).IsRequired();
            b.Property(e => e.BasePrice).HasPrecision(18, 4);
        });

        // Order
        mb.Entity<Order>(b => {
            b.HasKey(e => e.Id);
            b.HasIndex(e => e.OrderNumber).IsUnique();
            b.Property(e => e.Subtotal).HasPrecision(18, 4);
            b.Property(e => e.DiscountTotal).HasPrecision(18, 4);
            b.Property(e => e.TaxTotal).HasPrecision(18, 4);
            b.Property(e => e.GrandTotal).HasPrecision(18, 4);
        });

        // OrderLine
        mb.Entity<OrderLine>(b => {
            b.HasKey(e => e.Id);
            b.Property(e => e.UnitPrice).HasPrecision(18, 4);
            b.Property(e => e.OriginalUnitPrice).HasPrecision(18, 4);
            b.Property(e => e.LineTotal).HasPrecision(18, 4);
            b.Property(e => e.DiscountPercent).HasPrecision(5, 2);
        });

        // Price list
        mb.Entity<CompanyPriceList>(b => {
            b.HasKey(e => e.Id);
            b.Property(e => e.Name).HasMaxLength(200).IsRequired();
        });

        mb.Entity<PriceListLine>(b => {
            b.HasKey(e => e.Id);
            b.Property(e => e.UnitPrice).HasPrecision(18, 4);
            b.Property(e => e.DiscountPercent).HasPrecision(5, 2);
        });

        mb.Entity<VolumePriceTier>(b => {
            b.HasKey(e => e.Id);
            b.Property(e => e.UnitPrice).HasPrecision(18, 4);
        });

        mb.Entity<ContractPrice>(b => {
            b.HasKey(e => e.Id);
            b.Property(e => e.ContractUnitPrice).HasPrecision(18, 4);
        });

        mb.Entity<PromotionalDiscount>(b => {
            b.HasKey(e => e.Id);
            b.HasIndex(e => e.Code).IsUnique();
            b.Property(e => e.DiscountPercent).HasPrecision(5, 2);
            b.Property(e => e.MinOrderTotal).HasPrecision(18, 4);
        });

        mb.Entity<TaxRule>(b => {
            b.HasKey(e => e.Id);
            b.Property(e => e.TaxRate).HasPrecision(5, 4);
        });

        mb.Entity<CurrencyRate>(b => {
            b.HasKey(e => e.Id);
            b.Property(e => e.Rate).HasPrecision(18, 8);
        });

        // Query filter — soft delete simulation for Companies
        // Hidden behavior: deactivated companies still appear in some queries
        mb.Entity<Company>().HasQueryFilter(c => c.DeactivatedAt == null);

        // Deliberate: Article soft-delete NOT filtered at DB level — must be done in application layer
        // This is one of the hidden inconsistencies

        mb.Entity<CompanyProductAssignment>(b => {
            b.HasKey(e => e.Id);
            b.HasIndex(e => new { e.CompanyId, e.ArticleId }).IsUnique();
        });

        mb.Entity<CompanyCategoryAssignment>(b => {
            b.HasKey(e => e.Id);
            b.HasIndex(e => new { e.CompanyId, e.CategoryId }).IsUnique();
        });

        mb.Entity<ArticleProductGroupMembership>(b => {
            b.HasKey(e => e.Id);
            b.HasIndex(e => new { e.ArticleId, e.ProductGroupId }).IsUnique();
        });

        mb.Entity<CheckoutStepConfig>(b => {
            b.HasKey(e => e.Id);
            b.HasOne(e => e.CheckoutConfig)
                .WithMany(c => c.Steps)
                .HasForeignKey(e => e.CheckoutConfigId);
        });
    }
}
