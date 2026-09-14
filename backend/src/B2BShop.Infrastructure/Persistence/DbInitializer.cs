using B2BShop.Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace B2BShop.Infrastructure.Persistence;

/// <summary>
/// Creates schema and seeds data on first run.
/// </summary>
public static class DbInitializer
{
    public static async Task InitializeAsync(AppDbContext db, ILogger? logger = null, CancellationToken ct = default)
    {
        try
        {
            await db.Database.EnsureCreatedAsync(ct);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Schema creation failed");
            throw;
        }

        try
        {
            await ApplySchemaExtrasAsync(db, ct);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Schema extras could not be applied");
        }

        try
        {
            await DataSeeder.SeedAsync(db, ct);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Seeding failed");
            throw;
        }
    }

    private static async Task ApplySchemaExtrasAsync(AppDbContext db, CancellationToken ct)
    {
        var sqlPath = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "B2BShop.Infrastructure", "Migrations", "00_schema_extras.sql");

        if (!File.Exists(sqlPath))
        {
            // Try alternate path (deployed)
            sqlPath = Path.Combine(AppContext.BaseDirectory, "Migrations", "00_schema_extras.sql");
        }

        if (!File.Exists(sqlPath))
            return;

        var sql = await File.ReadAllTextAsync(sqlPath, ct);
        await db.Database.ExecuteSqlRawAsync(sql, ct);
    }
}
