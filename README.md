To create and apply migrations for the database, use the following commands:

```bash
dotnet ef migrations add 1.0.0.0 --project Bardcoded.ApiService\Bardcoded.ApiService.csproj --startup-project Bardcoded.ApiService\Bardcoded.ApiService.csproj

dotnet ef database update --project Bardcoded.ApiService\Bardcoded.ApiService.csproj --startup-project Bardcoded.ApiService\Bardcoded.ApiService.csproj 

```

These commands will scaffold the initial migration and update the SQLite database.

