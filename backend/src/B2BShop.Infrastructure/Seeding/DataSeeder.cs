using B2BShop.Domain.Entities;
using B2BShop.Domain.Enums;
using B2BShop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace B2BShop.Infrastructure.Seeding;

/// <summary>
/// Seeds the database with training data.
/// Deterministic (fixed GUIDs and random seed) — every provisioning produces the same dataset.
/// </summary>
public static class DataSeeder
{
    // Fixed random seed for reproducibility
    private static readonly Random Rng = new(42);

    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (await db.Companies.IgnoreQueryFilters().AnyAsync(ct))
            return; // Already seeded

        var categories = SeedCategories(db);
        await db.SaveChangesAsync(ct);

        var articles = SeedArticles(db, categories);
        await db.SaveChangesAsync(ct);

        var companies = SeedCompanies(db);
        await db.SaveChangesAsync(ct);

        var users = SeedUsers(db, companies);
        await db.SaveChangesAsync(ct);

        SeedAssortments(db, companies, articles, categories);
        await db.SaveChangesAsync(ct);

        SeedPricing(db, companies, articles);
        await db.SaveChangesAsync(ct);

        SeedCheckoutConfigs(db, companies);
        await db.SaveChangesAsync(ct);

        SeedIntegrationConfigs(db, companies);
        await db.SaveChangesAsync(ct);

        await SeedOrdersAsync(db, companies, users, articles, ct);
    }

    private static List<ProductCategory> SeedCategories(AppDbContext db)
    {
        var categories = new List<ProductCategory>
        {
            new() { Id = G("cat-001"), Code = "ELEC", Name = "Electronics", SortOrder = 1, IsActive = true },
            new() { Id = G("cat-002"), Code = "MECH", Name = "Mechanical Components", SortOrder = 2, IsActive = true },
            new() { Id = G("cat-003"), Code = "CHEM", Name = "Chemicals & Fluids", SortOrder = 3, IsActive = true },
            new() { Id = G("cat-004"), Code = "SAFE", Name = "Safety Equipment", SortOrder = 4, IsActive = true },
            new() { Id = G("cat-005"), Code = "TOOL", Name = "Tools & Instruments", SortOrder = 5, IsActive = true },
            new() { Id = G("cat-006"), Code = "PACK", Name = "Packaging Materials", SortOrder = 6, IsActive = true },
            new() { Id = G("cat-007"), Code = "FAST", Name = "Fasteners", SortOrder = 7, IsActive = true },
            new() { Id = G("cat-008"), Code = "HYDR", Name = "Hydraulics", SortOrder = 8, IsActive = true },
            new() { Id = G("cat-009"), Code = "PNEU", Name = "Pneumatics", SortOrder = 9, IsActive = true },
            new() { Id = G("cat-010"), Code = "LUBR", Name = "Lubricants", SortOrder = 10, IsActive = true }
        };
        db.Categories.AddRange(categories);
        return categories;
    }

    private static List<Article> SeedArticles(AppDbContext db, List<ProductCategory> categories)
    {
        var articles = new List<Article>();
        int counter = 1;

        foreach (var cat in categories)
        {
            // 50-60 articles per category = 500-600 total
            int articleCount = Rng.Next(50, 62);
            for (int i = 0; i < articleCount; i++)
            {
                var code = $"{cat.Code}-{counter:D4}";
                var price = Math.Round((decimal)(Rng.NextDouble() * 900 + 5), 2);
                var packSize = Rng.Next(1, 5) == 1 ? Rng.Next(2, 25) : 1;

                var article = new Article
                {
                    Id = Guid.NewGuid(),
                    Code = code,
                    Name = $"{cat.Name} Article {counter}",
                    Description = Rng.Next(5) == 0 ? null : $"Professional grade {cat.Name.ToLower()} component",
                    CategoryId = cat.Id,
                    BasePrice = price,
                    BaseCurrencyCode = "EUR",
                    UnitOfMeasure = cat.Code == "CHEM" || cat.Code == "LUBR" ? "LT" : "PCS",
                    PackSize = packSize,
                    EnforcePackSizeRounding = packSize > 1,
                    MinOrderQuantity = Rng.Next(10) == 0 ? Rng.Next(2, 10) : 1,
                    IsActive = Rng.Next(20) != 0,  // ~5% inactive
                    ImageUrl = $"/images/articles/{code.ToLower()}.jpg",
                    AttributesJson = $"{{\"weight\":\"{Rng.Next(1, 50)}kg\",\"material\":\"steel\"}}",
                    LegacyErpCode = $"ERP-{counter:D6}"  // Dead field
                };

                articles.Add(article);
                counter++;

                // Add stock level (Quantity is the field, AvailableQuantity is computed)
                db.StockLevels.Add(new ArticleStockLevel
                {
                    Id = Guid.NewGuid(),
                    ArticleId = article.Id,
                    WarehouseCode = "WH-MAIN",
                    Quantity = Rng.Next(0, 500),
                    ReservedQuantity = Rng.Next(0, 20),
                    UpdatedAt = DateTime.UtcNow.AddDays(-Rng.Next(1, 30))
                });
            }
        }

        db.Articles.AddRange(articles);

        // Edge case: article with very long name and unicode
        var edgeArticle = new Article
        {
            Id = G("art-edge-001"),
            Code = "EDGE-0001",
            Name = "LongName-" + new string('x', 140) + "-éàü",
            CategoryId = categories[0].Id,
            BasePrice = 0.01m,
            BaseCurrencyCode = "USD",
            UnitOfMeasure = "PCS",
            PackSize = 100,
            EnforcePackSizeRounding = true,
            MinOrderQuantity = 100,
            IsActive = true,
            AttributesJson = "{}"
        };
        articles.Add(edgeArticle);
        db.Articles.Add(edgeArticle);

        // Edge case: article assigned to zero companies (orphaned)
        var orphanedArticle = new Article
        {
            Id = G("art-edge-002"),
            Code = "EDGE-0002",
            Name = "Orphaned - Not Assigned",
            CategoryId = categories[1].Id,
            BasePrice = 99.99m,
            BaseCurrencyCode = "EUR",
            UnitOfMeasure = "PCS",
            PackSize = 1,
            IsActive = true,
            AttributesJson = "{}"
        };
        articles.Add(orphanedArticle);
        db.Articles.Add(orphanedArticle);

        return articles;
    }

    private static List<Company> SeedCompanies(AppDbContext db)
    {
        var companyA = new Company
        {
            Id = G("company-aaa"),
            Code = "ACME",
            Name = "Acme Industrial Supplies Ltd.",
            CurrencyCode = "EUR",
            LanguageCode = "en",
            IsActive = true,
            PricingEngineVersion = "legacy",
            CreditLimit = 50000m,
            CurrentCreditUsed = 12000m,
            EnableQuotes = true,
            EnableShoppingLists = true,
            EnableBulkUpload = true,
            CreatedAt = DateTime.UtcNow.AddYears(-2)
        };

        var companyB = new Company
        {
            Id = G("company-bbb"),
            Code = "METRO",
            Name = "Metro Parts & Components GmbH",
            CurrencyCode = "EUR",
            LanguageCode = "de",
            IsActive = true,
            PricingEngineVersion = "v2",
            CreditLimit = 200000m,
            CurrentCreditUsed = 45000m,
            EnableQuotes = true,
            EnableShoppingLists = true,
            EnableBulkUpload = false,
            CreatedAt = DateTime.UtcNow.AddYears(-1)
        };

        // Deactivated company — visible via IgnoreQueryFilters
        var companyC = new Company
        {
            Id = G("company-ccc"),
            Code = "LEGACY",
            Name = "Legacy Corp (Deactivated)",
            CurrencyCode = "USD",
            LanguageCode = "en",
            IsActive = false,
            PricingEngineVersion = "v2",
            DeactivatedAt = DateTime.UtcNow.AddMonths(-3),
            CreatedAt = DateTime.UtcNow.AddYears(-3)
        };

        db.Companies.Add(companyA);
        db.Companies.Add(companyB);
        db.Companies.Add(companyC);

        db.UiConfigurations.Add(new CompanyUiConfiguration
        {
            Id = G("ui-aaa"),
            CompanyId = companyA.Id,
            LogoUrl = "/assets/logos/acme.png",
            PrimaryColor = "#1e3a5f",
            SecondaryColor = "#c9a84c",
            CatalogLayout = "grid",
            ShowStockLevel = true,
            ShowProductCode = true,
            CustomLabelsJson = "{\"order\":\"Purchase Order\",\"cart\":\"Requisition\"}",
            EnabledSectionsJson = "[\"catalog\",\"orders\",\"approvals\",\"shopping-lists\",\"reports\"]",
            DateFormat = "DD/MM/YYYY",
            NumberFormat = "de-DE"
        });

        db.UiConfigurations.Add(new CompanyUiConfiguration
        {
            Id = G("ui-bbb"),
            CompanyId = companyB.Id,
            LogoUrl = "/assets/logos/metro.png",
            PrimaryColor = "#cc0000",
            SecondaryColor = "#ffffff",
            CatalogLayout = "table",
            ShowStockLevel = false,
            ShowProductCode = true,
            CustomLabelsJson = "{\"product\":\"Artikel\",\"quantity\":\"Menge\"}",
            EnabledSectionsJson = "[\"catalog\",\"orders\",\"quotes\",\"reports\",\"admin\"]",
            DateFormat = "DD.MM.YYYY",
            NumberFormat = "de-DE"
        });

        return new List<Company> { companyA, companyB, companyC };
    }

    private static List<User> SeedUsers(AppDbContext db, List<Company> companies)
    {
        var users = new List<User>();
        var companyA = companies[0];
        var companyB = companies[1];

        static string Hash(string pw) =>
            "$2a$10$" + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(pw));

        void Add(Guid id, string email, string name, UserRole role, Company co, Guid? acting = null)
        {
            var u = new User
            {
                Id = id,
                Email = email,
                PasswordHash = Hash("Password1!"),
                DisplayName = name,
                Role = role,
                CompanyId = co.Id,
                ActingOnBehalfOfCompanyId = acting,
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddMonths(-Rng.Next(1, 24))
            };
            users.Add(u);
            db.Users.Add(u);
        }

        Add(G("user-admin-1"), "admin@b2bshop.local", "Platform Administrator", UserRole.PlatformAdmin, companyA);
        Add(G("user-aaa-admin"), "admin@acme.example", "Alice Admin", UserRole.CompanyAdmin, companyA);
        Add(G("user-aaa-buyer1"), "buyer1@acme.example", "Bob Buyer", UserRole.Buyer, companyA);
        Add(G("user-aaa-buyer2"), "buyer2@acme.example", "Carol Buyer", UserRole.Buyer, companyA);
        Add(G("user-aaa-approver"), "approver@acme.example", "Dave Approver", UserRole.Approver, companyA);
        Add(G("user-aaa-auditor"), "auditor@acme.example", "Eve Auditor", UserRole.Auditor, companyA);
        Add(G("user-bbb-admin"), "admin@metro.example", "Franz Admin", UserRole.CompanyAdmin, companyB);
        Add(G("user-bbb-buyer1"), "buyer1@metro.example", "Greta Buyer", UserRole.Buyer, companyB);
        Add(G("user-bbb-approver"), "approver@metro.example", "Hans Approver", UserRole.Approver, companyB);
        Add(G("user-sales-1"), "salesrep@b2bshop.local", "Sandra Sales Rep", UserRole.SalesRep, companyA, companyA.Id);

        return users;
    }

    private static void SeedAssortments(AppDbContext db, List<Company> companies,
        List<Article> articles, List<ProductCategory> categories)
    {
        var companyA = companies[0];
        var companyB = companies[1];

        foreach (var cat in categories)
        {
            db.CategoryAssignments.Add(new CompanyCategoryAssignment
            {
                Id = Guid.NewGuid(),
                CompanyId = companyA.Id,
                CategoryId = cat.Id,
                IsVisible = true,
                IsExcluded = false,
                EffectiveFrom = DateTime.UtcNow.AddYears(-1)
            });
        }

        // Individually excluded articles
        foreach (var article in articles.Take(25))
        {
            db.ProductAssignments.Add(new CompanyProductAssignment
            {
                Id = Guid.NewGuid(),
                CompanyId = companyA.Id,
                ArticleId = article.Id,
                IsVisible = false,
                IsExcluded = true,
                IsOrderable = false
            });
        }

        // Edge case: visible but not orderable
        foreach (var article in articles.Skip(25).Take(10))
        {
            db.ProductAssignments.Add(new CompanyProductAssignment
            {
                Id = Guid.NewGuid(),
                CompanyId = companyA.Id,
                ArticleId = article.Id,
                IsVisible = true,
                IsOrderable = false,
                IsExcluded = false
            });
        }

        // Company B: only 3 categories, one expiring soon
        foreach (var (cat, idx) in categories.Take(3).Select((c, i) => (c, i)))
        {
            db.CategoryAssignments.Add(new CompanyCategoryAssignment
            {
                Id = Guid.NewGuid(),
                CompanyId = companyB.Id,
                CategoryId = cat.Id,
                IsVisible = true,
                IsExcluded = false,
                EffectiveFrom = DateTime.UtcNow.AddYears(-1),
                EffectiveTo = idx == 2 ? DateTime.UtcNow.AddDays(3) : null
            });
        }

        // Company B: a few individually-assigned articles from otherwise excluded category
        foreach (var article in articles.Where(a => a.CategoryId == categories[4].Id).Take(5))
        {
            db.ProductAssignments.Add(new CompanyProductAssignment
            {
                Id = Guid.NewGuid(),
                CompanyId = companyB.Id,
                ArticleId = article.Id,
                IsVisible = true,
                IsOrderable = true,
                IsExcluded = false
            });
        }
    }

    private static void SeedPricing(AppDbContext db, List<Company> companies, List<Article> articles)
    {
        var companyA = companies[0];
        var companyB = companies[1];
        var now = DateTime.UtcNow;

        var priceListA = new CompanyPriceList
        {
            Id = G("pl-aaa-001"),
            CompanyId = companyA.Id,
            Name = "ACME Standard 2024",
            CurrencyCode = "EUR",
            Priority = 1,
            IsActive = true,
            ValidFrom = now.AddYears(-1)
        };
        db.PriceLists.Add(priceListA);

        // PriceListLine requires UnitPrice
        foreach (var article in articles.Take(100))
        {
            db.PriceListLines.Add(new PriceListLine
            {
                Id = Guid.NewGuid(),
                PriceListId = priceListA.Id,
                ArticleId = article.Id,
                UnitPrice = article.BasePrice * 0.9m,
                CurrencyCode = "EUR",
                DiscountPercent = 10m
            });
        }

        // Volume tiers
        db.VolumePriceTiers.Add(new VolumePriceTier
        {
            Id = G("vt-aaa-001"),
            PriceListId = priceListA.Id,
            ArticleId = articles[0].Id,
            MinQuantity = 10,
            MaxQuantity = 49,
            UnitPrice = articles[0].BasePrice * 0.95m
        });
        db.VolumePriceTiers.Add(new VolumePriceTier
        {
            Id = G("vt-aaa-002"),
            PriceListId = priceListA.Id,
            ArticleId = articles[0].Id,
            MinQuantity = 50,
            MaxQuantity = null,
            UnitPrice = articles[0].BasePrice * 0.85m
        });

        var priceListB1 = new CompanyPriceList
        {
            Id = G("pl-bbb-001"),
            CompanyId = companyB.Id,
            Name = "Metro Special 2025",
            CurrencyCode = "EUR",
            Priority = 10,
            IsActive = true,
            ValidFrom = now.AddMonths(-6)
        };

        var priceListB2 = new CompanyPriceList
        {
            Id = G("pl-bbb-002"),
            CompanyId = companyB.Id,
            Name = "Metro Base 2024",
            CurrencyCode = "EUR",
            Priority = 5,
            IsActive = true,
            ValidFrom = now.AddYears(-1)
        };

        db.PriceLists.Add(priceListB1);
        db.PriceLists.Add(priceListB2);

        db.ContractPrices.Add(new ContractPrice
        {
            Id = G("cp-bbb-001"),
            CompanyId = companyB.Id,
            ArticleId = articles[10].Id,
            ContractUnitPrice = articles[10].BasePrice * 0.75m,
            CurrencyCode = "EUR",
            ContractReference = "CONTRACT-METRO-2025",
            ValidFrom = now.AddMonths(-3),
            ValidTo = now.AddMonths(9),
            AllowVolumeTierOverride = false
        });

        db.PromotionalDiscounts.Add(new PromotionalDiscount
        {
            Id = G("promo-001"),
            Code = "SUMMER2025",
            Name = "Summer 2025 Campaign",
            DiscountType = "OrderTotal",
            DiscountPercent = 5m,
            MinOrderTotal = 1000m,
            IsStackable = false,
            MaxUsesPerCompany = 10,
            IsActive = true,
            ValidFrom = now.AddMonths(-2),
            ValidTo = now.AddMonths(4)
        });

        db.CurrencyRates.Add(new CurrencyRate
        {
            Id = G("curr-001"),
            FromCurrency = "USD",
            ToCurrency = "EUR",
            Rate = 0.92m,
            EffectiveFrom = now.AddYears(-1),
            RoundingMode = "PerLine",
            MidpointRoundingRule = MidpointRounding.AwayFromZero
        });

        // Generic tax rule (Germany)
        db.TaxRules.Add(new TaxRule
        {
            Id = G("tax-001"),
            CountryCode = "DE",
            TaxRate = 0.19m,
            TaxType = "VAT",
            IsActive = true
        });

        // Company A: tax-exempt override (hidden business rule)
        db.TaxRules.Add(new TaxRule
        {
            Id = G("tax-aaa"),
            CountryCode = "GB",
            CompanyId = companyA.Id,
            TaxRate = 0m,
            TaxType = "VAT_EXEMPT",
            IsActive = true
        });
    }

    private static void SeedCheckoutConfigs(AppDbContext db, List<Company> companies)
    {
        var companyA = companies[0];
        var companyB = companies[1];

        var checkoutA = new CompanyCheckoutConfig
        {
            Id = G("checkout-aaa"),
            CompanyId = companyA.Id,
            RequiresApproval = true,
            ApprovalSpendThreshold = 5000m,
            AllowFreeAddressEntry = false,
            RequirePoNumber = true,
            PaymentTermsJson = "[\"Net 30\",\"Net 60\"]",
            ConfirmationChannel = "email"
        };
        db.CheckoutConfigs.Add(checkoutA);

        db.CheckoutSteps.AddRange(
            new CheckoutStepConfig { Id = G("step-aaa-1"), CheckoutConfigId = checkoutA.Id, StepType = "cart_review", Label = "Review Order", IsRequired = true, SortOrder = 1, ConfigJson = "{}" },
            new CheckoutStepConfig { Id = G("step-aaa-2"), CheckoutConfigId = checkoutA.Id, StepType = "delivery_address", Label = "Delivery Address", IsRequired = true, SortOrder = 2, ConfigJson = "{\"allowedAddressTypes\":[\"warehouse\",\"site\"]}" },
            new CheckoutStepConfig { Id = G("step-aaa-3"), CheckoutConfigId = checkoutA.Id, StepType = "po_number", Label = "PO Reference", IsRequired = true, SortOrder = 3, ConfigJson = "{\"maxLength\":20}" },
            new CheckoutStepConfig { Id = G("step-aaa-4"), CheckoutConfigId = checkoutA.Id, StepType = "payment_terms", Label = "Payment Terms", IsRequired = false, SortOrder = 4, ConfigJson = "{}" },
            new CheckoutStepConfig { Id = G("step-aaa-5"), CheckoutConfigId = checkoutA.Id, StepType = "confirmation", Label = "Confirm Order", IsRequired = true, SortOrder = 5, ConfigJson = "{}" }
        );

        var checkoutB = new CompanyCheckoutConfig
        {
            Id = G("checkout-bbb"),
            CompanyId = companyB.Id,
            RequiresApproval = false,
            AllowFreeAddressEntry = true,
            RequirePoNumber = false,
            PaymentTermsJson = "[\"Prepayment\",\"Net 14\"]",
            ConfirmationChannel = "portal"
        };
        db.CheckoutConfigs.Add(checkoutB);

        db.CheckoutSteps.AddRange(
            new CheckoutStepConfig { Id = G("step-bbb-1"), CheckoutConfigId = checkoutB.Id, StepType = "cart_review", Label = "Warenkorb prüfen", IsRequired = true, SortOrder = 1, ConfigJson = "{}" },
            new CheckoutStepConfig { Id = G("step-bbb-2"), CheckoutConfigId = checkoutB.Id, StepType = "delivery_address", Label = "Lieferadresse", IsRequired = true, SortOrder = 2, ConfigJson = "{\"allowFreeEntry\":true}" },
            new CheckoutStepConfig { Id = G("step-bbb-3"), CheckoutConfigId = checkoutB.Id, StepType = "confirmation", Label = "Bestätigung", IsRequired = true, SortOrder = 3, ConfigJson = "{}" }
        );

        // Delivery addresses (DeliveryAddress has Label not Name, CountryCode not Country)
        db.DeliveryAddresses.AddRange(
            new DeliveryAddress { Id = G("addr-aaa-1"), CompanyId = companyA.Id, Label = "Main Warehouse", Street = "Industrial Park 1", City = "London", PostalCode = "E1 1AA", CountryCode = "GB", IsActive = true },
            new DeliveryAddress { Id = G("addr-aaa-2"), CompanyId = companyA.Id, Label = "Site B", Street = "Factory Lane 5", City = "Manchester", PostalCode = "M1 1AB", CountryCode = "GB", IsActive = true },
            new DeliveryAddress { Id = G("addr-bbb-1"), CompanyId = companyB.Id, Label = "Hauptlager", Street = "Industriestraße 10", City = "München", PostalCode = "80331", CountryCode = "DE", IsActive = true }
        );
    }

    private static void SeedIntegrationConfigs(AppDbContext db, List<Company> companies)
    {
        db.IntegrationConfigs.Add(new IntegrationConfig
        {
            Id = G("int-aaa"),
            CompanyId = companies[0].Id,
            Transport = "http",
            Format = "json",
            Schedule = "immediate",
            RetryCount = 3,
            RetryDelaySeconds = 60,
            IsActive = true,
            SimulatedFailurePercent = 10,
            SimulatedDelayMs = 500,
            TransportConfigJson = "{\"endpoint\":\"http://stub.local/orders\",\"method\":\"POST\"}",
            FieldMappingJson = "{\"OrderNumber\":\"order_ref\"}"
        });

        db.IntegrationConfigs.Add(new IntegrationConfig
        {
            Id = G("int-bbb"),
            CompanyId = companies[1].Id,
            Transport = "sftp",
            Format = "xml",
            Schedule = "batch",
            BatchCronExpression = "0 6 * * *",
            RetryCount = 5,
            RetryDelaySeconds = 300,
            IsActive = true,
            SimulatedFailurePercent = 0,
            SimulatedDelayMs = 2000,
            TransportConfigJson = "{\"host\":\"sftp.stub.local\",\"port\":22}",
            FieldMappingJson = "{}"
        });
    }

    private static async Task SeedOrdersAsync(AppDbContext db, List<Company> companies,
        List<User> users, List<Article> articles, CancellationToken ct)
    {
        var companyA = companies[0];
        var companyB = companies[1];
        var buyerA = users.First(u => u.CompanyId == companyA.Id && u.Role == UserRole.Buyer);
        var buyerB = users.First(u => u.CompanyId == companyB.Id && u.Role == UserRole.Buyer);
        var activeArticles = articles.Where(a => a.IsActive).ToList();

        var statusSequence = new[] {
            OrderStatus.Fulfilled, OrderStatus.Fulfilled, OrderStatus.Fulfilled,
            OrderStatus.Transferred, OrderStatus.Acknowledged, OrderStatus.PartiallyFulfilled,
            OrderStatus.PendingApproval, OrderStatus.Approved,
            OrderStatus.Cancelled, OrderStatus.Draft
        };

        for (int i = 0; i < 600; i++)
        {
            var status = statusSequence[i % statusSequence.Length];
            var orderArticles = activeArticles.Skip(i % 50).Take(Rng.Next(1, 6)).ToList();
            var createdAt = DateTime.UtcNow.AddDays(-Rng.Next(1, 730));

            var lines = orderArticles.Select(a => new OrderLine
            {
                Id = Guid.NewGuid(),
                ArticleId = a.Id,
                ArticleCode = a.Code,
                ArticleName = a.Name,
                Quantity = Rng.Next(1, 50),
                UnitOfMeasure = a.UnitOfMeasure,
                UnitPrice = a.BasePrice,
                OriginalUnitPrice = a.BasePrice,
                LineTotal = a.BasePrice * Rng.Next(1, 50)
            }).ToList();
            decimal subtotal = lines.Sum(l => l.LineTotal);

            db.Orders.Add(new Order
            {
                Id = Guid.NewGuid(),
                OrderNumber = $"ORD-{createdAt:yyyyMM}-A{i + 1:D5}",
                CompanyId = companyA.Id,
                UserId = buyerA.Id,
                Status = status,
                Lines = lines,
                Subtotal = subtotal,
                DiscountTotal = 0,
                TaxTotal = 0,
                GrandTotal = subtotal,
                CurrencyCode = "EUR",
                ApprovalRequired = subtotal >= 5000,
                PricingEngineVersion = "legacy",
                CreatedAt = createdAt,
                UpdatedAt = createdAt.AddHours(Rng.Next(0, 48)),
                TransferredAt = status >= OrderStatus.Transferred ? createdAt.AddHours(2) : null,
                AcknowledgedAt = status >= OrderStatus.Acknowledged ? createdAt.AddHours(4) : null
            });

            if (i % 100 == 99)
                await db.SaveChangesAsync(ct);
        }

        for (int i = 0; i < 400; i++)
        {
            var status = statusSequence[i % statusSequence.Length];
            var orderArticles = activeArticles.Skip(i % 30).Take(Rng.Next(1, 4)).ToList();
            var createdAt = DateTime.UtcNow.AddDays(-Rng.Next(1, 365));

            var lines = orderArticles.Select(a => new OrderLine
            {
                Id = Guid.NewGuid(),
                ArticleId = a.Id,
                ArticleCode = a.Code,
                ArticleName = a.Name,
                Quantity = Rng.Next(1, 20),
                UnitOfMeasure = a.UnitOfMeasure,
                UnitPrice = a.BasePrice * 0.9m,
                OriginalUnitPrice = a.BasePrice,
                LineTotal = a.BasePrice * 0.9m * Rng.Next(1, 20)
            }).ToList();
            decimal subtotal = lines.Sum(l => l.LineTotal);

            db.Orders.Add(new Order
            {
                Id = Guid.NewGuid(),
                OrderNumber = $"ORD-{createdAt:yyyyMM}-B{i + 1:D5}",
                CompanyId = companyB.Id,
                UserId = buyerB.Id,
                Status = status,
                Lines = lines,
                Subtotal = subtotal,
                DiscountTotal = subtotal * 0.1m,
                TaxTotal = subtotal * 0.19m,
                GrandTotal = subtotal * 1.09m,
                CurrencyCode = "EUR",
                ApprovalRequired = false,
                PricingEngineVersion = "v2",
                CreatedAt = createdAt,
                UpdatedAt = createdAt.AddHours(1)
            });

            if (i % 100 == 99)
                await db.SaveChangesAsync(ct);
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Deterministic GUID from a string seed (MD5-based).</summary>
    private static Guid G(string seed)
    {
        var bytes = System.Security.Cryptography.MD5.HashData(
            System.Text.Encoding.UTF8.GetBytes(seed));
        return new Guid(bytes);
    }
}
