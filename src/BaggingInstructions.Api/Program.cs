using Microsoft.EntityFrameworkCore;
using BaggingInstructions.Api.Core;
using BaggingInstructions.Api.Services;
using PdfSharp.Fonts;

// Npgsql 6.0+ の DateTime 扱いを従来互換にする（timestamp without time zone として扱う）
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

// 汁仕分表 PDF 用: 日本語フォントを .ttf で提供するカスタム解決（PdfSharp は TTC 非対応のため）
try
{
    if (GlobalFontSettings.FontResolver == null)
        GlobalFontSettings.FontResolver = new JuicePdfFontResolver();
}
catch { /* 既にフォント使用後は無視 */ }

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var conn = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
    ?? "Host=localhost;Port=5432;Database=ROZECDB;Username=rozec;Password=***";
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(conn));

var connOther = builder.Configuration.GetConnectionString("CraftlineaxOther")
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__CraftlineaxOther")
    ?? "Host=localhost;Port=5432;Database=craftlineaxother;Username=rozec;Password=***";
builder.Services.AddDbContext<CstmeatDbContext>(options =>
    options.UseNpgsql(connOther));

builder.Services.AddScoped<SearchService>();
builder.Services.AddScoped<StockService>();
builder.Services.AddScoped<BaggingCalculatorService>();
builder.Services.AddScoped<JuicePdfService>();
builder.Services.AddScoped<PreparationWorkService>();
builder.Services.AddScoped<PreparationWorkPdfService>();
builder.Services.AddScoped<AggregateSummaryService>();
builder.Services.AddScoped<AggregateSummaryPdfService>();
builder.Services.AddScoped<DeliveryNoteService>();
builder.Services.AddScoped<DeliveryNotePdfService>();
builder.Services.AddScoped<PersonalDeliveryService>();
builder.Services.AddScoped<PersonalDeliveryPdfService>();
builder.Services.AddScoped<CookingInstructionService>();
builder.Services.AddScoped<CookingInstructionPdfService>();
builder.Services.AddScoped<ProductionInstructionService>();
builder.Services.AddScoped<ProductionInstructionPdfService>();
builder.Services.AddScoped<InspectionRecordService>();
builder.Services.AddScoped<InspectionRecordPdfService>();
builder.Services.AddScoped<AcceptanceRecordService>();
builder.Services.AddScoped<AcceptanceRecordPdfService>();

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

// 汁仕分表.rxz テンプレートを返す（静的ファイル404対策）
app.MapGet("/api/templates/juice", (IWebHostEnvironment env) =>
{
    var path = Path.Combine(env.ContentRootPath, "..", "..", "static", "templates", "汁仕分表.rxz");
    var fullPath = Path.GetFullPath(path);
    if (!File.Exists(fullPath))
        return Results.NotFound();
    return Results.File(fullPath, "application/xml", "汁仕分表.rxz");
});

app.MapGet("/api/templates/preparation-work", (IWebHostEnvironment env) =>
{
    var path = Path.Combine(env.ContentRootPath, "..", "..", "static", "templates", "作業前準備書.rxz");
    var fullPath = Path.GetFullPath(path);
    if (!File.Exists(fullPath))
        return Results.NotFound();
    return Results.File(fullPath, "application/xml", "作業前準備書.rxz");
});

// ルート "/" はフロントの index へリダイレクト（/static/index.html を参照するため）
app.MapGet("/", () => Results.Redirect("/static/index.html", permanent: false));

// 静的ファイル（現行の /static に合わせる）
app.UseDefaultFiles();
app.UseStaticFiles();
var staticPath = Path.Combine(app.Environment.ContentRootPath, "..", "..", "static");
if (Directory.Exists(staticPath))
    app.UseStaticFiles(new StaticFileOptions { FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(Path.GetFullPath(staticPath)), RequestPath = "/static" });

app.Run();
