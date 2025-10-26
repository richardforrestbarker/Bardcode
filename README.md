# Bardcode - Home Supply Management System

Bardcode is a home supply management system that helps you track and manage household items using barcode scanning. The system allows you to scan product barcodes, retrieve product information, and maintain an inventory of your home supplies.

## Features

- **Barcode Scanning**: Scan product barcodes using your device's camera
- **Product Information Retrieval**: Automatically fetch product details from external APIs
- **Inventory Management**: Track and manage your home supplies
- **Web-Based Interface**: Access your inventory from any device with a modern web browser

## Technology Stack

- **.NET 9.0**: Backend API and hosting
- **Blazor WebAssembly**: Progressive web application frontend
- **Entity Framework Core**: Database access with SQLite
- **ASP.NET Core**: RESTful API services
- **.NET Aspire**: Distributed application orchestration

## Prerequisites

Before building and running the project, ensure you have the following installed:

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) or later
- [LibMan CLI](https://learn.microsoft.com/en-us/aspnet/core/client-side/libman/libman-cli) (for managing client-side libraries)
- [Entity Framework Core tools](https://learn.microsoft.com/en-us/ef/core/cli/dotnet) (for database migrations)

### Installing LibMan CLI

To install the LibMan CLI tool globally, run:

```bash
dotnet tool install -g Microsoft.Web.LibraryManager.Cli
```

### Installing Entity Framework Core Tools

To install the EF Core CLI tools globally, run:

```bash
dotnet tool install -g dotnet-ef
```

Alternatively, the EF Core tools are included as a project reference and can be used via `dotnet ef` without global installation.

## Building the Project

Follow these steps to build the project:

### 1. Clone the Repository

```bash
git clone https://github.com/richardforrestbarker/Bardcode.git
cd Bardcode
```

### 2. Restore NuGet Packages

```bash
dotnet restore
```

### 3. Restore Client-Side Libraries with LibMan

The Blazor WebAssembly project uses LibMan to manage client-side libraries (jQuery, Bootstrap, Quagga.js, and Popper.js). Restore these libraries by running:

```bash
cd Bardcoded.Wasm
libman restore
cd ..
```

This will download the required client libraries specified in `Bardcoded.Wasm/libman.json` to the `wwwroot/lib` directory.

> **Note**: If you encounter version resolution errors with libman, you may need to update the library versions in `Bardcoded.Wasm/libman.json` to match what's currently available on the cdnjs provider.

### 4. Build the Solution

```bash
dotnet build
```

## Database Setup

The project uses Entity Framework Core with SQLite for data storage. Follow these steps to set up the database:

### Creating Migrations

To create a new migration, use the following command:

```bash
dotnet ef migrations add <MigrationName> --project Bardcoded.ApiService/Bardcoded.ApiService.csproj --startup-project Bardcoded.ApiService/Bardcoded.ApiService.csproj
```

Replace `<MigrationName>` with a descriptive name for your migration (e.g., "InitialCreate" or version number like "1.0.0.0").

### Applying Migrations

To apply migrations and update the SQLite database, run:

```bash
dotnet ef database update --project Bardcoded.ApiService/Bardcoded.ApiService.csproj --startup-project Bardcoded.ApiService/Bardcoded.ApiService.csproj
```

This command will create the database file and apply all pending migrations.

## Running the Application

The application uses .NET Aspire for orchestration, which manages multiple services including the API service, web frontend, and Redis cache.

### Using .NET Aspire (Recommended)

To run the entire application with all services:

```bash
dotnet run --project Bardcoded.AppHost/Bardcoded.AppHost.csproj
```

This will start:
- The API service (Bardcoded.ApiService)
- The Blazor WebAssembly frontend (Bardcoded.Wasm)
- A Redis cache instance
- The Aspire dashboard for monitoring

### Running Individual Services

Alternatively, you can run services individually:

**API Service:**
```bash
dotnet run --project Bardcoded.ApiService/Bardcoded.ApiService.csproj
```

**Web Frontend:**
```bash
dotnet run --project Bardcoded.Wasm/Bardcoded.Wasm.csproj
```

## Running Tests

To run the test suite:

```bash
dotnet test
```

## Project Structure

- **Bardcoded.ApiService**: RESTful API backend service
- **Bardcoded.Wasm**: Blazor WebAssembly frontend application
- **Bardcoded.Data**: Shared data models and DTOs
- **Bardcoded.AppHost**: .NET Aspire orchestration host
- **Bardcoded.ServiceDefaults**: Shared service configuration
- **Bardcoded.Tests**: Unit and integration tests

## Configuration

Configuration files are located in:
- `Bardcoded.ApiService/appsettings.json` - API service configuration
- `Bardcoded.Wasm/wwwroot/appsettings.json` - Frontend configuration

### Barcode Data Providers

Bardcode uses external APIs to retrieve product information from barcodes. The system supports multiple barcode data providers that are configured in `Bardcoded.ApiService/appsettings.json` under the `Application.Integrations` section.

#### Supported Providers

**Setting the API Key (Security Best Practice):**

**IMPORTANT:** Never store API keys directly in configuration files as this is a security risk. Instead, set the API key using an environment variable or command-line argument.

**Option 1: Environment Variable (Recommended)**

Set the environment variable using the hierarchical configuration key format:

```bash
# Linux/macOS
export Application__Integrations__0__key="your-api-key-here"

# Windows (Command Prompt)
set Application__Integrations__0__key=your-api-key-here

# Windows (PowerShell)
$env:Application__Integrations__0__key="your-api-key-here"
```

**Note:** The index `0` corresponds to the first provider in the `Integrations` array. Adjust the index based on the provider's position in your configuration.

**Option 2: Command-Line Argument**

When running the application, pass the API key as a command-line argument:

```bash
dotnet run --project Bardcoded.ApiService/Bardcoded.ApiService.csproj --Application:Integrations:0:key="your-api-key-here"
```

**Option 3: User Secrets (Development Only)**

For local development, use the .NET user secrets feature:

```bash
cd Bardcoded.ApiService
dotnet user-secrets set "Application:Integrations:0:key" "your-api-key-here"
```

The application includes three barcode data providers, each with different features, costs, and requirements:

##### 1. UPC Database

**Website:** https://upcdatabase.org/

**Features:**
- Supports UPC barcodes
- Requires API key authentication
- Provides product titles, descriptions, and images

**Account Setup:**
1. Create an account at https://upcdatabase.org/
2. Navigate to your account settings to generate an API key
3. Copy your API key

**Rate Limits & Pricing:**
- Free tier: 100 requests per day
- Paid plans available with higher limits
- See https://upcdatabase.org/api for current pricing and rate limits

**License:**
- Review the terms of service at https://upcdatabase.org/terms

**Configuration:**

The UPC Database provider is pre-configured in `Bardcoded.ApiService/appsettings.json` with an empty `key` field:

```json
{
  "$type": "UpcDatabaseApiProvider",
  "url": "https://api.upcdatabase.org",
  "path": "product/{barcode}",
  "key": "",
  "allowedBarcodeTypes": [ "UPC" ]
}
```

##### 2. Open Food Facts

**Website:** https://world.openfoodfacts.org/

**Features:**
- Free and open database
- No API key required
- Supports EAN-13, EAN-8, UPC-A, UPC-E barcodes
- Primarily focused on food products
- Community-driven database

**Account Setup:**
- No account or API key required
- Optional: Create an account to contribute product data

**Rate Limits & Pricing:**
- Completely free
- Fair use policy: Please be respectful of the API and avoid excessive requests
- See https://world.openfoodfacts.org/data for API documentation

**License:**
- Open Database License (ODbL)
- Data is freely available
- Read more at https://world.openfoodfacts.org/terms-of-use

**Configuration:**

The default configuration in `Bardcoded.ApiService/appsettings.json` works without modification:

```json
{
  "$type": "OpenFoodFactsApiProvider",
  "url": "https://world.openfoodfacts.org",
  "path": "api/v2/product/{barcode}.json",
  "key": "",
  "allowedBarcodeTypes": [ "EAN-13", "EAN-8", "UPC-A", "UPC-E" ]
}
```

No API key is needed for Open Food Facts.

##### 3. Barcode Lookup

**Website:** https://www.barcodelookup.com/

**Features:**
- Comprehensive barcode database
- Supports UPC-A, UPC-E, EAN-13, EAN-8, ISBN-10, ISBN-13
- Provides detailed product information including features, images, and metadata
- Commercial-grade API

**Account Setup:**
1. Create an account at https://www.barcodelookup.com/
2. Sign up for an API plan at https://www.barcodelookup.com/api
3. Copy your API key from your account dashboard

**Rate Limits & Pricing:**
- Free tier: Limited requests per month (check current limits)
- Paid plans: Various tiers with different rate limits
- See https://www.barcodelookup.com/api#plans for current pricing
- Rate limit documentation: https://www.barcodelookup.com/api#rate-limiting

**License:**
- Commercial API with terms of service
- Review the API License Agreement at https://www.barcodelookup.com/api#license
- End User License Agreement: https://www.barcodelookup.com/eula

**Configuration:**

The Barcode Lookup provider is pre-configured in `Bardcoded.ApiService/appsettings.json` with an empty `key` field:

```json
{
  "$type": "BarcodeLookupApiProvider",
  "url": "https://api.barcodelookup.com",
  "path": "v3/products?barcode={barcode}&key=",
  "key": "",
  "allowedBarcodeTypes": [ "UPC-A", "UPC-E", "EAN-13", "EAN-8", "ISBN-10", "ISBN-13" ]
}
```

**Setting the API Key (Security Best Practice):**

**IMPORTANT:** Never store API keys directly in configuration files as this is a security risk. Instead, set the API key using an environment variable or command-line argument.

**Option 1: Environment Variable (Recommended)**

Set the environment variable using the hierarchical configuration key format:

```bash
# Linux/macOS
export Application__Integrations__2__key="your-api-key-here"

# Windows (Command Prompt)
set Application__Integrations__2__key=your-api-key-here

# Windows (PowerShell)
$env:Application__Integrations__2__key="your-api-key-here"
```

**Note:** The index `2` corresponds to the third provider in the `Integrations` array (Barcode Lookup is third by default). Adjust the index based on the provider's position in your configuration.

**Option 2: Command-Line Argument**

When running the application, pass the API key as a command-line argument:

```bash
dotnet run --project Bardcoded.ApiService/Bardcoded.ApiService.csproj --Application:Integrations:2:key="your-api-key-here"
```

**Option 3: User Secrets (Development Only)**

For local development, use the .NET user secrets feature:

```bash
cd Bardcoded.ApiService
dotnet user-secrets set "Application:Integrations:2:key" "your-api-key-here"
```

#### Provider Priority

The system queries providers in the order they appear in the configuration file. Once a provider successfully returns product data, subsequent providers are not queried. You can reorder the providers in `appsettings.json` to change the priority.

#### Disabling Providers

To disable a provider, you can either:
- Remove it from the `Application.Integrations` array in `appsettings.json`
- Leave the `key` field empty (for providers that require authentication)

#### Example Complete Configuration

Here's an example of the complete configuration in `appsettings.json` with all three providers. **Note:** The `key` fields should remain empty in the configuration file.

```json
"Application": {
  "Integrations": [
    {
      "$type": "OpenFoodFactsApiProvider",
      "url": "https://world.openfoodfacts.org",
      "path": "api/v2/product/{barcode}.json",
      "key": "",
      "allowedBarcodeTypes": [ "EAN-13", "EAN-8", "UPC-A", "UPC-E" ]
    },
    {
      "$type": "UpcDatabaseApiProvider",
      "url": "https://api.upcdatabase.org",
      "path": "product/{barcode}",
      "key": "",
      "allowedBarcodeTypes": [ "UPC" ]
    },
    {
      "$type": "BarcodeLookupApiProvider",
      "url": "https://api.barcodelookup.com",
      "path": "v3/products?barcode={barcode}&key=",
      "key": "",
      "allowedBarcodeTypes": [ "UPC-A", "UPC-E", "EAN-13", "EAN-8", "ISBN-10", "ISBN-13" ]
    }
  ],
  "Features": {
    "FetchFromApis": true,
    "UseDatabase": true,
    "UseCache": false
  }
}
```

**Setting API Keys Securely:**

Set the API keys using environment variables instead of hardcoding them in the configuration:

```bash
# Set UPC Database API key (index 1)
export Application__Integrations__1__key="your-upcdatabase-key"

# Set Barcode Lookup API key (index 2)
export Application__Integrations__2__key="your-barcodelookup-key"
```

In this example, Open Food Facts is queried first (free and no authentication required), followed by UPC Database, and finally Barcode Lookup.

## Authentication and Authorization

Bardcode uses ASP.NET Core Identity for user authentication and authorization. The Identity system is configured with its own separate database and uses GUIDs for all Identity-related entity IDs.

### Enabling/Disabling Authentication

Authentication and authorization can be toggled using the `AuthNZ` feature flag in the application configuration:

```json
"Application": {
  "Features": {
    "FetchFromApis": true,
    "UseDatabase": true,
    "UseCache": false,
    "AuthNZ": false
  }
}
```

**Important Notes:**
- **Default**: `AuthNZ` is set to `false` by default (authentication disabled)
- **Development**: `AuthNZ` is set to `true` in `appsettings.Development.json` to enable authentication during development
- **Production**: Should be set to `true` for production environments
- **Tests**: Default value of `false` ensures tests run without authentication requirements

When `AuthNZ` is disabled:
- All API endpoints are accessible without authentication
- Identity endpoints (`/identity/*`) are not mapped
- User seeding does not occur
- No authentication middleware is applied

When `AuthNZ` is enabled:
- All barcode endpoints require authentication
- User management endpoints require Owner or Admin role
- Identity API endpoints are available at `/identity`
- Default owner and admin users are created on startup

### Identity Configuration

Identity settings are configured in `Bardcoded.ApiService/appsettings.json` under the `Identity` section:

```json
"Identity": {
  "Password": {
    "RequiredLength": 8,
    "RequireDigit": true,
    "RequireLowercase": true,
    "RequireUppercase": true,
    "RequireNonAlphanumeric": true,
    "RequiredUniqueChars": 1
  },
  "Lockout": {
    "DefaultLockoutTimeSpan": "00:05:00",
    "MaxFailedAccessAttempts": 5,
    "AllowedForNewUsers": true
  },
  "User": {
    "RequireUniqueEmail": true
  },
  "SignIn": {
    "RequireConfirmedEmail": false,
    "RequireConfirmedPhoneNumber": false,
    "RequireConfirmedAccount": false
  }
}
```

#### Password Requirements

- **RequiredLength**: Minimum password length (default: 8 characters)
- **RequireDigit**: Password must contain at least one digit (0-9)
- **RequireLowercase**: Password must contain at least one lowercase letter (a-z)
- **RequireUppercase**: Password must contain at least one uppercase letter (A-Z)
- **RequireNonAlphanumeric**: Password must contain at least one special character
- **RequiredUniqueChars**: Minimum number of unique characters in the password

#### Lockout Settings

- **DefaultLockoutTimeSpan**: Duration a user is locked out after exceeding failed login attempts (default: 5 minutes)
- **MaxFailedAccessAttempts**: Number of failed login attempts before lockout (default: 5)
- **AllowedForNewUsers**: Whether lockout applies to new users (default: true)

#### User Settings

- **RequireUniqueEmail**: Each email address can only be associated with one account (default: true)

#### Sign-In Settings

- **RequireConfirmedEmail**: Whether users must confirm their email before signing in (default: false)
- **RequireConfirmedPhoneNumber**: Whether users must confirm their phone number (default: false)
- **RequireConfirmedAccount**: Whether accounts must be confirmed before use (default: false)

### Database Configuration

The Identity system uses a separate SQLite database from the main barcode database. The connection strings are configured in `appsettings.json`:

```json
"ConnectionStrings": {
  "Barcode": "Data Source=bardcode.db",
  "Identity": "Data Source=identity.db"
}
```

You can change the Identity connection string to use a different database provider (SQL Server, PostgreSQL, etc.) by updating the connection string and modifying the `Program.cs` to use the appropriate Entity Framework provider.

### Default Users

Two default users are automatically created when the application starts if they don't already exist:

#### Owner Account
- **Username**: `owner`
- **Email**: `owner@bardcode.local`
- **Default Password**: `Owner@123456` (can be configured in appsettings.json)
- **Role**: Owner
- **Permissions**: Full access to user management and all application features
- **Tagline**: "Owner account for system administration"

#### Admin Account
- **Username**: `admin`
- **Email**: `admin@bardcode.local`
- **Default Password**: `Admin@123456` (can be configured in appsettings.json)
- **Role**: Admin
- **Permissions**: Access to user management and all application features
- **Tagline**: "Admin account for user management"

**IMPORTANT**: Change these default passwords immediately in a production environment!

### Configuring Default User Passwords

To change the default passwords for owner and admin users, update the configuration in `appsettings.json`:

```json
"Identity": {
  "DefaultUsers": {
    "Owner": {
      "Password": "YourSecureOwnerPassword123!"
    },
    "Admin": {
      "Password": "YourSecureAdminPassword123!"
    }
  }
}
```

Alternatively, use environment variables or user secrets for better security:

```bash
# Using environment variables
export Identity__DefaultUsers__Owner__Password="YourSecureOwnerPassword123!"
export Identity__DefaultUsers__Admin__Password="YourSecureAdminPassword123!"

# Using .NET user secrets (development only)
dotnet user-secrets set "Identity:DefaultUsers:Owner:Password" "YourSecureOwnerPassword123!"
dotnet user-secrets set "Identity:DefaultUsers:Admin:Password" "YourSecureAdminPassword123!"
```

### User Taglines

The ApplicationUser model extends ASP.NET Core Identity's IdentityUser with an additional `Tagline` property. This allows users to set a custom tagline to verify they're on the correct site (similar to a security phrase).

### Creating New Users

Users can register through the Identity API endpoints at `/identity/register`. The registration endpoint accepts the following JSON:

```json
{
  "email": "user@example.com",
  "password": "SecurePassword123!"
}
```

### Managing Users

User management is restricted to users with the `Owner` or `Admin` role. The following endpoints are available at `/users`:

- **GET /users**: List all users
- **GET /users/{id}**: Get a specific user by ID
- **DELETE /users/{id}**: Delete a user

### Authorization Policies

The application uses the following authorization policies:

- **Default Policy**: All API endpoints require authentication (except Identity endpoints)
- **UserManagement Policy**: Restricted to users with the `Owner` or `Admin` role

### Identity API Endpoints

The application exposes the following Identity API endpoints under `/identity`:

- **POST /identity/register**: Register a new user account
- **POST /identity/login**: Log in with email and password
- **POST /identity/refresh**: Refresh authentication token
- **POST /identity/confirmEmail**: Confirm email address
- **POST /identity/resendConfirmationEmail**: Resend email confirmation
- **POST /identity/forgotPassword**: Request password reset
- **POST /identity/resetPassword**: Reset password with token
- **POST /identity/manage/2fa**: Manage two-factor authentication
- **GET /identity/manage/info**: Get user information
- **POST /identity/manage/info**: Update user information

For detailed API documentation, run the application in development mode and access the OpenAPI/Swagger documentation.

### Database Migrations

To create Identity database migrations:

```bash
cd Bardcoded.ApiService
dotnet ef migrations add MigrationName --context ApplicationDbContext
dotnet ef database update --context ApplicationDbContext
```

The Identity database is automatically created and seeded with default users when the application starts.

## License

[Specify your license here]

## Contributing

[Add contribution guidelines if applicable]
