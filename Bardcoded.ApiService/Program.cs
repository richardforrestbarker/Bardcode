using Bardcoded.ApiService.Data;
using Bardcoded.ApiService.Data.Identity;
using Bardcoded.ApiService.Providers;
using Bardcoded.ApiService.Services;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FeatureManagement;
using Microsoft.OpenApi.Validations;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();
builder.Services.AddFeatureManagement(builder.Configuration.GetRequiredSection("Application:Features"));
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("Barcode") ?? "";
var identityConnectionString = builder.Configuration.GetConnectionString("Identity") ?? "Data Source=identity.db";

var corsconfig = builder.Configuration.GetRequiredSection("Cors").Get<Dictionary<string, CorsPolicy>>();

var integrations = builder.Configuration.GetRequiredSection("Application:Integrations").Get<IEnumerable<ApiProviderConfiguration>>();
if (!integrations.Any())
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
builder.Services.AddCors();

// Check if AuthNZ feature is enabled
var authNZEnabled = builder.Configuration.GetValue<bool>("Application:Features:AuthNZ", false);

if (authNZEnabled)
{
    builder.Services.AddTransient<IEmailSender<ApplicationUser>, NoOpEmailSender>();
    // Add Identity services
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlite(identityConnectionString));

    builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
    {
        // Password settings from configuration
        var passwordConfig = builder.Configuration.GetSection("Identity:Password");
        options.Password.RequiredLength = passwordConfig.GetValue<int>("RequiredLength", 8);
        options.Password.RequireDigit = passwordConfig.GetValue<bool>("RequireDigit", true);
        options.Password.RequireLowercase = passwordConfig.GetValue<bool>("RequireLowercase", true);
        options.Password.RequireUppercase = passwordConfig.GetValue<bool>("RequireUppercase", true);
        options.Password.RequireNonAlphanumeric = passwordConfig.GetValue<bool>("RequireNonAlphanumeric", true);
        options.Password.RequiredUniqueChars = passwordConfig.GetValue<int>("RequiredUniqueChars", 1);

        // Lockout settings from configuration
        var lockoutConfig = builder.Configuration.GetSection("Identity:Lockout");
        options.Lockout.DefaultLockoutTimeSpan = lockoutConfig.GetValue<TimeSpan>("DefaultLockoutTimeSpan", TimeSpan.FromMinutes(5));
        options.Lockout.MaxFailedAccessAttempts = lockoutConfig.GetValue<int>("MaxFailedAccessAttempts", 5);
        options.Lockout.AllowedForNewUsers = lockoutConfig.GetValue<bool>("AllowedForNewUsers", true);

        // User settings from configuration
        var userConfig = builder.Configuration.GetSection("Identity:User");
        options.User.RequireUniqueEmail = userConfig.GetValue<bool>("RequireUniqueEmail", true);

        // SignIn settings from configuration
        var signInConfig = builder.Configuration.GetSection("Identity:SignIn");
        options.SignIn.RequireConfirmedEmail = signInConfig.GetValue<bool>("RequireConfirmedEmail", false);
        options.SignIn.RequireConfirmedPhoneNumber = signInConfig.GetValue<bool>("RequireConfirmedPhoneNumber", false);
        options.SignIn.RequireConfirmedAccount = signInConfig.GetValue<bool>("RequireConfirmedAccount", false);
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

    builder.Services.AddScoped<IdentitySeeder>();

    builder.Services.AddAuthentication();
    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("UserManagement", policy => 
            policy.RequireRole("Owner", "Admin"));
    });
    
}
else
{
    // Add minimal authentication/authorization for no-auth mode
    builder.Services.AddAuthentication();
    builder.Services.AddAuthorization();
}

var app = builder.Build();

// Seed Identity database only if AuthNZ is enabled
if (authNZEnabled)
{
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        try
        {
            var identityContext = services.GetRequiredService<ApplicationDbContext>();
            await identityContext.Database.EnsureCreatedAsync();
            
            var seeder = services.GetRequiredService<IdentitySeeder>();
            await seeder.SeedAsync();
        }
        catch (Exception ex)
        {
            var logger = services.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "An error occurred while seeding the Identity database.");
        }
    }
}

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

app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints();
app.MapControllers();

// Map Identity endpoints only if AuthNZ is enabled
if (authNZEnabled)
{
    app.MapGroup("/identity").MapIdentityApi<ApplicationUser>();
}

app.Run();

