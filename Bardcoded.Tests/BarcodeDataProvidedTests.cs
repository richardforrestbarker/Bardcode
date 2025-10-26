using Bardcoded.ApiService.Data;
using Bardcoded.ApiService.Data.Store;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bardcoded.Tests;

[Trait("unit", "BarcodeDataContext")]
public class BarcodeDataProvidedTests
{
    private BarcodeDataContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<BarcodeDataContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new BarcodeDataContext(options);
    }

    [Fact]
    public async Task InsertBarcodeDataProvided_CreatesEntry()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var barcode = new BarcodeData
        {
            Bard = "123456789",
            Name = "Test Product",
            Description = "Test Description",
            Base64Image = "base64data",
            ImageType = "png",
            Source = "API"
        };
        await context.InsertBarcode(barcode);

        var providerData = new BarcodeDataProvided
        {
            Bard = "123456789",
            LastUpdated = DateTime.UtcNow,
            ProviderType = "OpenFoodFacts",
            ProviderJson = "{\"status\":1,\"product\":{\"name\":\"Test\"}}"
        };

        // Act
        context.InsertBarcodeDataProvided(providerData);

        // Assert
        var retrieved = await context.GetBarcodeDataProvided("123456789");
        Assert.NotNull(retrieved);
        Assert.Equal("123456789", retrieved.Bard);
        Assert.Equal("OpenFoodFacts", retrieved.ProviderType);
        Assert.Contains("Test", retrieved.ProviderJson);
    }

    [Fact]
    public async Task GetBarcodeDataProvided_ReturnsNullWhenNotFound()
    {
        // Arrange
        using var context = CreateInMemoryContext();

        // Act
        var result = await context.GetBarcodeDataProvided("nonexistent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task BarcodeDataProvided_CascadeDeletesWithBarcode()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var barcode = new BarcodeData
        {
            Bard = "123456789",
            Name = "Test Product",
            Description = "Test Description",
            Base64Image = "base64data",
            ImageType = "png",
            Source = "API"
        };
        await context.InsertBarcode(barcode);

        var providerData = new BarcodeDataProvided
        {
            Bard = "123456789",
            LastUpdated = DateTime.UtcNow,
            ProviderType = "OpenFoodFacts",
            ProviderJson = "{\"status\":1}"
        };
        context.InsertBarcodeDataProvided(providerData);

        // Act
        await context.DeleteBarcode("123456789");

        // Assert
        var retrievedProvider = await context.GetBarcodeDataProvided("123456789");
        Assert.Null(retrievedProvider);
    }

    [Fact]
    public async Task InsertBarcodeDataProvided_UpdatesLastUpdatedTime()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var barcode = new BarcodeData
        {
            Bard = "987654321",
            Name = "Another Product",
            Description = "Description",
            Base64Image = "base64",
            ImageType = "jpg",
            Source = "API"
        };
        await context.InsertBarcode(barcode);

        var beforeTime = DateTime.UtcNow;
        var providerData = new BarcodeDataProvided
        {
            Bard = "987654321",
            LastUpdated = beforeTime,
            ProviderType = "UpcDatabase",
            ProviderJson = "{\"item\":\"data\"}"
        };

        // Act
        context.InsertBarcodeDataProvided(providerData);

        // Assert
        var retrieved = await context.GetBarcodeDataProvided("987654321");
        Assert.NotNull(retrieved);
        Assert.Equal(beforeTime, retrieved.LastUpdated);
        Assert.Equal("UpcDatabase", retrieved.ProviderType);
    }

    [Fact]
    public async Task BarcodeDataProvided_StoresFullJsonData()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var complexJson = @"{
            ""status"": 1,
            ""product"": {
                ""name"": ""Complex Product"",
                ""brands"": ""Brand Name"",
                ""categories"": ""Category1,Category2"",
                ""images"": [""url1"", ""url2""]
            }
        }";

        var barcode = new BarcodeData
        {
            Bard = "111222333",
            Name = "Product",
            Description = "Desc",
            Base64Image = "img",
            ImageType = "png",
            Source = "API"
        };
        await context.InsertBarcode(barcode);

        var providerData = new BarcodeDataProvided
        {
            Bard = "111222333",
            LastUpdated = DateTime.UtcNow,
            ProviderType = "BarcodeLookup",
            ProviderJson = complexJson
        };

        // Act
        context.InsertBarcodeDataProvided(providerData);

        // Assert
        var retrieved = await context.GetBarcodeDataProvided("111222333");
        Assert.NotNull(retrieved);
        Assert.Equal(complexJson, retrieved.ProviderJson);
        Assert.Contains("Complex Product", retrieved.ProviderJson);
    }
}
