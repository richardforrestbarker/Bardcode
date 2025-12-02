using Bardcoded.ApiService.Data;
using Bardcoded.ApiService.Providers;
using Bardcoded.ApiService.Controllers;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.FeatureManagement;
using DocumentProcessor.Data;
using DocumentProcessor.Api.Ocr;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();
builder.Services.AddFeatureManagement(builder.Configuration.GetRequiredSection("Application:Features"));
builder.Services.AddControllers()
                .AddOcrControllers();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("Barcode") ?? "";

var corsconfig = builder.Configuration.GetRequiredSection("Cors").Get<Dictionary<string, CorsPolicy>>();

var integrations = builder.Configuration.GetRequiredSection("Application:Integrations").Get<IEnumerable<ApiProviderConfiguration>>();
if (!integrations?.Any() ?? true)
{
    Console.WriteLine("No API provider configs found. If the feature is on, it still won't work.");
}
Dictionary<ApiProviderConfiguration, ApiProvider> providers = new();
foreach (var integration in integrations ?? Enumerable.Empty<ApiProviderConfiguration>())
{

    switch (integration.Type)
    {
        case nameof(OpenFoodFactsApiProvider):
            builder.Services.AddScoped(sp => new OpenFoodFactsApiProvider(integration));
            providers.Add(integration, new OpenFoodFactsApiProvider(integration));
            break;
        case nameof(UpcDatabaseApiProvider):
            builder.Services.AddScoped(sp => new UpcDatabaseApiProvider(integration));
            providers.Add(integration, new UpcDatabaseApiProvider(integration));
            break;
        case nameof(BarcodeLookupApiProvider):
            builder.Services.AddScoped(sp => new BarcodeLookupApiProvider(integration));
            providers.Add(integration, new BarcodeLookupApiProvider(integration));
            break;
        default:
            throw new NotSupportedException($"API provider type '{integration.Type}' is not supported.");
    }

}
builder.Services.AddSingleton(providers);
builder.Services.AddHostedService<RateResetService>();
builder.Services.AddDbContext<IBarcodeDataContext, BarcodeDataContext>(options => options.UseSqlite(connectionString));
builder.Services.AddSingleton<MemoryCache>();
builder.Services.AddTransient<BarcodeFetcher>();

// Configure OCR settings (legacy receipt processor)
var ocrConfig = builder.Configuration.GetSection("Ocr").Get<OcrConfiguration>() ?? new OcrConfiguration();
builder.Services.AddSingleton(ocrConfig);
builder.Services.AddSingleton<IReceiptProcessor, ReceiptProcessor>();

// Add OCR document processing services (isolated for future extraction)
builder.Services.AddOcrDocumentProcessing(builder.Configuration)
    ;

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

