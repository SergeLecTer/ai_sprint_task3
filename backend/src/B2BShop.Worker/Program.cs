using B2BShop.Infrastructure.Persistence;
using B2BShop.Infrastructure.Services;
using B2BShop.Worker;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

// Database — same connection string as the API
builder.Services.AddDbContext<AppDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Transfer service (scoped, created per poll cycle)
builder.Services.AddScoped<OrderTransferService>();

// Background worker
builder.Services.AddHostedService<OrderTransferWorker>();

var host = builder.Build();
host.Run();
