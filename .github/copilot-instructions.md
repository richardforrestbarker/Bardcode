# Bardcode - Copilot Instructions

## Application Overview

Bardcode is a home supply management system built with .NET 9.0 that helps users track and manage household items using barcode scanning. The system allows users to scan product barcodes using their device's camera, automatically retrieve product information from external APIs, and maintain an inventory of home supplies. The application uses a progressive web application (PWA) architecture with Blazor WebAssembly for the frontend and ASP.NET Core for the backend API.

### Key Features
- **Barcode Scanning**: Real-time barcode scanning using device camera via Quagga.js
- **Product Information Retrieval**: Automatic product data fetching from multiple external APIs (Open Food Facts, UPC Database, Barcode Lookup)
- **Inventory Management**: Track and manage home supply inventory with SQLite database
- **Web-Based Interface**: Modern, responsive Blazor WebAssembly PWA accessible from any device
- **Distributed Architecture**: Uses .NET Aspire for orchestration with Redis caching support

## Directory Structure

### Root Level
- **Bardcoded.sln** - Visual Studio solution file containing all projects
- **README.md** - Comprehensive project documentation with setup and usage instructions

### Project Structure

#### **Bardcoded.ApiService**
RESTful API backend service providing barcode and inventory management endpoints.
- **Controllers/** - API controllers for handling HTTP requests
  - `ItemsController.cs` - Inventory item management endpoints
- **Data/** - Database context and entity definitions
  - `IBarcodeDataContext.cs` - Interface for database operations
  - `Store/Entities.cs` - Entity Framework Core data models
- **Migrations/** - Entity Framework Core database migrations
- **Providers/** - External API integration providers
  - `Providers.cs` - Provider implementations (OpenFoodFacts, UpcDatabase, BarcodeLookup)
  - `BarcodeFetcher.cs` - Service for fetching barcode data from providers
  - `RateResetService.cs` - Background service for managing API rate limits
- **Program.cs** - Application entry point and service configuration
- **appsettings.json** - Configuration including API provider settings

#### **Bardcoded.Wasm**
Blazor WebAssembly progressive web application frontend.
- **Components/** - Reusable Blazor components
  - `Barcode/BarcodeReader.razor` - Barcode scanner component using Quagga.js
  - `ProductResolver.razor` - Product information resolution and display
  - `Notification.razor` - Toast notification component
- **Pages/** - Blazor page components
  - `Home.razor` - Landing page
  - `NewProduct.razor` - Add new product to inventory
  - `Inventory.razor` - View and manage inventory
- **Layout/** - Application layout components
- **Clients/** - HTTP client services for API communication
  - `BardcodeApiHttpClient.cs` - Typed HTTP client for backend API
- **wwwroot/** - Static web assets, including libraries managed by LibMan
- **Program.cs** - WASM application entry point

#### **Bardcoded.Data**
Shared data transfer objects (DTOs) and data models used across projects.
- **Api/** - API provider configuration models
- **Messages/** - Data transfer objects for API communication
  - `BarcodeView.cs` - DTO for barcode product information
  - `BarcodeMetadata.cs` - Metadata models for barcode data
- **Exceptions/** - Custom exception types
- **BardcodedApiConfiguration.cs** - API configuration models

#### **Bardcoded.AppHost**
.NET Aspire orchestration host for managing distributed services.
- Configures and runs API service, WASM frontend, and Redis cache
- Provides Aspire dashboard for monitoring

#### **Bardcoded.ServiceDefaults**
Shared service configuration and defaults used by Aspire-based services.
- Common service configurations
- Health checks
- Telemetry setup

#### **Bardcoded.Tests**
Unit and integration tests using xUnit.
- Test files following `<ClassName>Tests.cs` naming convention
- **TestData/** - Test data files and fixtures
- Uses xUnit v3, Aspire.Hosting.Testing, and Entity Framework In-Memory provider

## Required Conventions and Best Practices

### Testing Frameworks

#### **Prefer xUnit for all unit and integration tests**
- Use xUnit v3 as the primary testing framework
- Organize tests with `[Fact]` for single test cases and `[Theory]` with `[InlineData]` or `[MemberData]` for parameterized tests
- Use `[Trait]` attributes to categorize tests (e.g., `[Trait("unit", "ApiClient")]`)

#### **Use Playwright for end-to-end and browser testing**
- Playwright should be used for automated browser testing and UI interactions
- Test barcode scanning functionality, navigation flows, and user interactions

#### **Use bUnit for Blazor component testing**
- bUnit is the preferred framework for testing Blazor components in isolation
- Test component rendering, parameter binding, event handling, and lifecycle hooks

### Testing Conventions

#### **Parameterized Tests**
Prefer parameterized tests using `[Theory]` with `TheoryData<>` or `[InlineData]` to cover as many edge cases as feasible in a single test method.

**Example:**
```csharp
public static TheoryData<string, bool> ResponseStatusData => new()
{
    { @"{""status"":1,""code"":""3017620422003"",""product"":{""product_name"":""Nutella""}}", true },
    { @"{""status"":0,""status_verbose"":""product not found""}", false },
    { @"{""status"":1,""code"":""123456789"",""product"":null}", false }
};

[Theory]
[MemberData(nameof(ResponseStatusData))]
public async Task Translate_HandlesVariousResponseStatuses(string jsonResponse, bool shouldReturnProduct)
{
    // Test implementation
}
```

#### **Fakes Over Mocks**
- **Use fakes and fake data instead of mocking frameworks** whenever possible
- Create concrete test implementations of interfaces rather than using mock objects
- Generate realistic test data for more meaningful tests
- Use in-memory implementations (e.g., Entity Framework InMemory provider) for database testing

**Example:**
```csharp
// Good: Using fake data
private HttpResponseMessage CreateHttpResponseMessage(string content)
{
    return new HttpResponseMessage
    {
        StatusCode = HttpStatusCode.OK,
        Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json")
    };
}

// Good: Using in-memory database
builder.Services.AddDbContext<IBarcodeDataContext, BarcodeDataContext>(
    options => options.UseInMemoryDatabase("TestDb"));
```

### C# and .NET Best Practices

#### **Prefer Interfaces**
- Define interfaces for services, repositories, and data contexts
- Use dependency injection with interface-based registrations
- This enables testability and loose coupling

**Example:**
```csharp
// Define interface
public interface IBarcodeDataContext
{
    Task<List<BarcodeData>> GetAll();
    Task<BarcodeData> GetBarcode(string barcode);
}

// Implement concrete class
public class BarcodeDataContext : DbContext, IBarcodeDataContext
{
    // Implementation
}

// Register in DI
builder.Services.AddDbContext<IBarcodeDataContext, BarcodeDataContext>(
    options => options.UseSqlite(connectionString));
```

#### **Nullable Reference Types**
- Enable nullable reference types (`<Nullable>enable</Nullable>` in .csproj)
- Properly annotate nullable and non-nullable references
- Use `required` modifier or nullable types (`?`) appropriately to avoid CS8618 warnings

#### **Async/Await Patterns**
- Use async/await for all I/O-bound operations (database, HTTP calls)
- Return `Task<T>` from async methods, not `Task<T?>` unless null is a valid return value
- Use `ConfigureAwait(false)` in library code when appropriate

#### **Primary Constructors (C# 12)**
- Use primary constructors for simple dependency injection scenarios
- Example: `public class MyService(ILogger<MyService> logger)`

### Entity Framework Core Best Practices

#### **Database Context**
- Use interfaces for DbContext to enable testing and decoupling
- Configure entity relationships in `OnModelCreating`
- Use appropriate `DeleteBehavior` for cascading operations

**Example:**
```csharp
protected override void OnModelCreating(ModelBuilder builder)
{
    base.OnModelCreating(builder);
    
    builder.Entity<BarcodeDataProvided>()
        .HasKey(b => b.Bard);
    
    builder.Entity<BarcodeDataProvided>()
        .HasOne<BarcodeData>()
        .WithOne()
        .HasForeignKey<BarcodeDataProvided>(b => b.Bard)
        .OnDelete(DeleteBehavior.Cascade);
}
```

#### **Migrations**
- Create migrations with descriptive names or version numbers
- Use the pattern: `dotnet ef migrations add <MigrationName> --project <ProjectPath>`
- Always review generated migrations before applying

#### **Query Patterns**
- Use `SingleOrDefaultAsync()` when expecting zero or one result and want an exception if more than one exists
- Use `FirstAsync()` when expecting at least one result (throws if none found) and want only the first one
- Use `FirstOrDefaultAsync()` when expecting zero or more results and want only the first one (or default if none)
- Use `ToListAsync()` for retrieving collections
- Avoid `ToList()` on IQueryable - use async equivalents

### Blazor Best Practices

#### **Component Structure**
- Separate concerns: UI markup in `.razor`, logic in code-behind or partial classes
- Use `@inject` directive for dependency injection in components
- Use `[Parameter]` attribute for component parameters
- Use `[CascadingParameter]` for values passed down component hierarchies

#### **State Management**
- Use component parameters for parent-child communication
- Use services for shared state across components
- Consider `LocalStorage` for client-side persistence (see `LocalStorageAccessor.cs`)

#### **JavaScript Interop**
- Use `IJSRuntime` for calling JavaScript from C#
- Implement proper error handling for JS interop calls
- Ensure JavaScript libraries are loaded before calling functions

**Example:**
```csharp
@inject IJSRuntime JSRuntime

protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (firstRender)
    {
        await JSRuntime.InvokeVoidAsync("initializeScanner");
    }
}
```

#### **Client-Side Libraries**
- Use LibMan for managing client-side libraries (Bootstrap, jQuery, Quagga.js, Popper.js)
- Libraries are defined in `libman.json` and restored to `wwwroot/lib`
- Run `libman restore` to download libraries after cloning

### API and HTTP Best Practices

#### **RESTful Design**
- Use appropriate HTTP verbs (GET, POST, PUT, DELETE)
- Return proper HTTP status codes
- Use DTOs for request/response bodies
- Implement proper error handling with `ProblemDetails`

#### **Configuration**
- Store configuration in `appsettings.json`
- **Never commit secrets or API keys to source control**
- Use user secrets for development: `dotnet user-secrets set "Key" "Value"`
- Use environment variables for production: `Application__Integrations__0__key`

#### **API Client Pattern**
- Use typed HttpClient with dependency injection
- Configure base address and default headers
- Use JSON serialization options consistently

**Example:**
```csharp
builder.Services.AddHttpClient<BardcodeApiHttpClient>(client =>
{
    client.BaseAddress = new Uri("https://api.example.com");
});
```

### Dependency Injection

#### **Service Lifetimes**
- **Singleton**: Services that maintain state across the application lifetime (e.g., `MemoryCache`)
- **Scoped**: Services that should be created per request (e.g., `DbContext`)
- **Transient**: Services that should be created each time they're requested (e.g., `BarcodeFetcher`)

**Example from Program.cs:**
```csharp
builder.Services.AddSingleton<MemoryCache>();
builder.Services.AddDbContext<IBarcodeDataContext, BarcodeDataContext>(options => options.UseSqlite(connectionString));
builder.Services.AddTransient<BarcodeFetcher>();
```

### Code Quality and Style

#### **Error Handling**
- Use try-catch blocks for operations that may throw exceptions
- Log errors appropriately
- Return meaningful error messages to clients
- Use custom exception types when appropriate (see `Bardcoded.Data/Exceptions`)

#### **Logging**
- Use structured logging with `ILogger<T>`
- Log at appropriate levels (Debug, Information, Warning, Error, Critical)
- Include context in log messages

#### **Code Organization**
- Keep classes focused and single-purpose
- Use regions sparingly, prefer well-named methods
- Follow SOLID principles
- Use meaningful variable and method names

### Security Best Practices

#### **API Keys and Secrets**
- Never hardcode API keys or connection strings
- Use configuration providers (user secrets, environment variables, Azure Key Vault)
- Validate and sanitize all user input
- Use CORS appropriately to restrict API access

#### **Data Validation**
- Validate input at API boundaries
- Use data annotations on DTOs
- Implement proper authentication and authorization when needed

### .NET Aspire Conventions

#### **Service Orchestration**
- Use `builder.AddServiceDefaults()` for standard service configuration
- Configure services with Aspire service discovery
- Use Redis for caching with Aspire integration
- Monitor services via Aspire dashboard in development

### Documentation

#### **Code Comments**
- Use XML documentation comments for public APIs
- Document complex algorithms or business logic
- Keep comments up-to-date with code changes

#### **README Updates**
- Update README.md when adding new features or changing setup procedures
- Document new dependencies and their purpose
- Provide clear setup instructions for new developers

## Summary of Key Conventions

1. **Testing**: Use xUnit for unit tests, Playwright for E2E, bUnit for Blazor components
2. **Test Design**: Prefer parameterized tests with TheoryData; use fakes over mocks
3. **Architecture**: Prefer interfaces for dependency injection and testability
4. **Data Access**: Use Entity Framework Core with async patterns and interface-based contexts
5. **Frontend**: Follow Blazor best practices with proper component structure and state management
6. **Configuration**: Never commit secrets; use user secrets or environment variables
7. **Code Quality**: Write clean, testable code following SOLID principles
8. **Security**: Validate inputs, protect secrets, implement proper error handling

## Development Workflow

1. **Setup**: Run `dotnet restore`, `libman restore`, create database with `dotnet ef database update`
2. **Build**: Use `dotnet build` to compile the solution
3. **Test**: Use `dotnet test` to run all tests
4. **Run**: Use `dotnet run --project Bardcoded.AppHost` to start all services via Aspire
5. **Database Changes**: Create migrations, review them, and apply with `dotnet ef` commands
6. **Client Libraries**: Update `libman.json` and run `libman restore` when adding new libraries
