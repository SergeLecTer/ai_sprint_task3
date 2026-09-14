using B2BShop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace B2BShop.Tests.Infrastructure;

/// <summary>
/// Verifies that EF Core migration snapshot compiles and model is self-consistent.
/// Does not require a real DB connection.
/// </summary>
public class MigrationTests
{
    [Fact]
    public void DbContext_CanBuildModel_WithoutConnection()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var db = new AppDbContext(opts);

        // Validates that the model is consistent (no shadow key issues, etc.)
        var model = db.Model;
        Assert.NotNull(model);

        var entityTypes = model.GetEntityTypes().ToList();
        Assert.True(entityTypes.Count > 10, $"Expected >10 entity types, got {entityTypes.Count}");
    }

    [Fact]
    public void Orders_HasRequiredProperties()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var db = new AppDbContext(opts);
        var orderType = db.Model.FindEntityType(typeof(B2BShop.Domain.Entities.Order));

        Assert.NotNull(orderType);
        Assert.NotNull(orderType.FindProperty("OrderNumber"));
        Assert.NotNull(orderType.FindProperty("Status"));
        Assert.NotNull(orderType.FindProperty("GrandTotal"));
    }

    [Fact]
    public void Company_HasQueryFilter_ForSoftDelete()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var db = new AppDbContext(opts);
        var companyType = db.Model.FindEntityType(typeof(B2BShop.Domain.Entities.Company));

        Assert.NotNull(companyType);
        // Company should have query filter (soft delete via DeactivatedAt)
        var queryFilters = companyType.GetDeclaredQueryFilters();
        Assert.True(queryFilters.Any(), "Company should have a query filter for soft-delete");
    }
}
