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

## License

[Specify your license here]

## Contributing

[Add contribution guidelines if applicable]
