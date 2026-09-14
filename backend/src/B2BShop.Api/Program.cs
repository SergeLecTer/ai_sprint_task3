using System.Text;
using B2BShop.Application.Catalog;
using B2BShop.Application.Pricing;
using B2BShop.Infrastructure.Persistence;
using B2BShop.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<AppDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Application services
builder.Services.AddScoped<PricingEngineSelector>();
builder.Services.AddScoped<PricingEngineV2>();
builder.Services.AddScoped<AssortmentEngine>();
builder.Services.AddScoped<CatalogService>();
builder.Services.AddScoped<OrderService>();

// Caching
builder.Services.AddMemoryCache();
builder.Services.AddDistributedMemoryCache();

// JWT Auth
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? "fallback-secret-for-dev-only";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts => {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// CORS for local dev
builder.Services.AddCors(opts => opts.AddDefaultPolicy(p =>
    p.WithOrigins("http://localhost:3000").AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

// Auto-migrate on startup (dev convenience)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var initLogger = scope.ServiceProvider.GetService<ILogger<AppDbContext>>();
    await DbInitializer.InitializeAsync(db, initLogger);
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
