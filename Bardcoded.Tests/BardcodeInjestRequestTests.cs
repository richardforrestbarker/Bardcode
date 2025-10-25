using Bardcoded.Data.Messages;
using Xunit;

namespace Bardcoded.Tests;

[Trait("unit", "BardcodeInjestRequest")]
public class BardcodeInjestRequestTests
{
    [Fact]
    public void BardcodeInjestRequest_CanStoreProviderData()
    {
        // Arrange
        var request = new BardcodeInjestRequest
        {
            Bard = "123456789",
            Name = "Test Product",
            Description = "Test Description",
            Base64Image = "base64data",
            ImageType = "png",
            Source = "API",
            WeightVolume = "500g",
            ProviderType = "OpenFoodFacts",
            ProviderJson = "{\"status\":1,\"product\":{\"name\":\"Test\"}}"
        };

        // Assert
        Assert.Equal("123456789", request.Bard);
        Assert.Equal("OpenFoodFacts", request.ProviderType);
        Assert.Contains("Test", request.ProviderJson);
    }

    [Fact]
    public void BardcodeInjestRequest_ProviderFieldsAreOptional()
    {
        // Arrange
        var request = new BardcodeInjestRequest
        {
            Bard = "987654321",
            Name = "Another Product",
            Description = "Description",
            Base64Image = "base64",
            ImageType = "jpg",
            Source = "Manual",
            WeightVolume = "1L"
        };

        // Assert
        Assert.Null(request.ProviderType);
        Assert.Null(request.ProviderJson);
    }

    [Fact]
    public void BardcodeInjestRequest_CanStoreComplexJson()
    {
        // Arrange
        var complexJson = @"{
            ""status"": 1,
            ""product"": {
                ""name"": ""Complex Product"",
                ""brands"": ""Brand Name"",
                ""categories"": ""Category1,Category2""
            }
        }";

        var request = new BardcodeInjestRequest
        {
            Bard = "111222333",
            Name = "Product",
            Description = "Desc",
            Base64Image = "img",
            ImageType = "png",
            Source = "API",
            WeightVolume = "200ml",
            ProviderType = "BarcodeLookup",
            ProviderJson = complexJson
        };

        // Assert
        Assert.Equal(complexJson, request.ProviderJson);
        Assert.Contains("Complex Product", request.ProviderJson);
        Assert.Equal("BarcodeLookup", request.ProviderType);
    }
}
