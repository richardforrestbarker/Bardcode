using Bardcoded.ApiService.Data;
using Bardcoded.ApiService.Providers;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.FeatureManagement;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();
builder.Services.AddFeatureManagement(builder.Configuration.GetRequiredSection("Application:Features"));
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("Barcode") ?? "";

var corsconfig = builder.Configuration.GetRequiredSection("Cors").Get<Dictionary<string, CorsPolicy>>();

var integrations = builder.Configuration.GetRequiredSection("Application:Integrations").Get<List<ApiProviderConfiguration>>();
if (integrations == null || integrations.Count == 0)
{
    Console.WriteLine("No API provider configs found. If the feature is on, it still won't work.");
}
builder.Services.AddSingleton(sc => integrations ?? new List<ApiProviderConfiguration>());

builder.Services.AddDbContext<BarcodeDataContext>(options => options.UseSqlite(connectionString));
builder.Services.AddDbContext<IBarcodeDataContext, BarcodeDataContext>(options => options.UseSqlite(connectionString));
builder.Services.AddSingleton<MemoryCache>();
builder.Services.AddTransient<BarcodeFetcher>();
builder.Services.AddCors();

var app = builder.Build();
app.UsePathBase("/");
// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
app.UseCors(c =>
{
    var policy = new CorsPolicy();
    foreach (var kvp in corsconfig)
    {
        c.WithOrigins(kvp.Value.Origins.ToArray());
        c.WithMethods("*");
        c.WithHeaders("*");
        c.WithExposedHeaders("*");

        var x = c.Build();
    }
});

app.MapDefaultEndpoints();
app.MapControllers();
app.Run();

