using Microsoft.EntityFrameworkCore;
using BaggingInstructions.Api.Core;
using BaggingInstructions.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var conn = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
    ?? "Host=localhost;Port=5432;Database=ROZECDB;Username=rozec;Password=***";
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(conn, npgsql => npgsql.EnableLegacyTimestampBehavior()));

builder.Services.AddScoped<SearchService>();
builder.Services.AddScoped<StockService>();
builder.Services.AddScoped<BaggingCalculatorService>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseCors();
app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.MapGet("/health", (IConfiguration config) => new
{
    status = "ok",
    environment = config["Environment"] ?? "Development"
});

// 静的ファイル（現行の /static に合わせる）
app.UseDefaultFiles();
app.UseStaticFiles();
var staticPath = Path.Combine(app.Environment.ContentRootPath, "..", "..", "static");
if (Directory.Exists(staticPath))
    app.UseStaticFiles(new StaticFileOptions { FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(Path.GetFullPath(staticPath)), RequestPath = "/static" });

app.Run();
