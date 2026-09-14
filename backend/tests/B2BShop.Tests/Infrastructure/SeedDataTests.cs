using B2BShop.Infrastructure.Persistence;
using B2BShop.Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore;

namespace B2BShop.Tests.Infrastructure;

/// <summary>
/// Verifies seed data composition. Weak test — does not check business rule correctness of seed data.
/// Deliberately uses InMemory DB so it runs without PostgreSQL.
/// </summary>
public class SeedDataTests
{
    private static AppDbContext BuildDb()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(opts);
    }

    [Fact]
    public async Task Seed_CreatesAtLeast3Companies()
    {
        using var db = BuildDb();
        await DataSeeder.SeedAsync(db);

        var count = await db.Companies.IgnoreQueryFilters().CountAsync();
        Assert.True(count >= 3, $"Expected at least 3 companies, got {count}");
    }

    [Fact]
    public async Task Seed_Creates500PlusArticles()
    {
        using var db = BuildDb();
        await DataSeeder.SeedAsync(db);

        var count = await db.Articles.CountAsync();
        Assert.True(count >= 500, $"Expected at least 500 articles, got {count}");
    }

    [Fact]
    public async Task Seed_CreatesUsersForAllRoles()
    {
        using var db = BuildDb();
        await DataSeeder.SeedAsync(db);

        var users = await db.Users.ToListAsync();
        var roles = users.Select(u => u.Role).Distinct().ToList();
        // Should have at least buyer, admin, approver
        Assert.True(roles.Count >= 3, $"Expected at least 3 roles covered, got {roles.Count}");
    }

    [Fact]
    public async Task Seed_CreatesOrdersInMultipleStates()
    {
        using var db = BuildDb();
        await DataSeeder.SeedAsync(db);

        var orders = await db.Orders.ToListAsync();
        var statuses = orders.Select(o => o.Status).Distinct().ToList();
        Assert.True(statuses.Count >= 3, $"Expected orders in at least 3 statuses, got {statuses.Count}");
    }

    [Fact]
    public async Task Seed_CreatesEdgeCaseData()
    {
        using var db = BuildDb();
        await DataSeeder.SeedAsync(db);

        // Edge case: article visible but not orderable
        var notOrderable = await db.ProductAssignments
            .CountAsync(a => a.IsVisible && !a.IsOrderable);
        Assert.True(notOrderable > 0, "Expected at least one article that is visible but not orderable");
    }
}
