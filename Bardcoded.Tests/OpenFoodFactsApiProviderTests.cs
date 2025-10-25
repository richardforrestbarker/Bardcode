using Bardcoded.ApiService.Providers;
using Bardcoded.Data.Api;
using Bardcoded.Data.Messages;
using System.Net;
using System.Text.Json;
using Xunit;

namespace Bardcoded.Tests;

public class OpenFoodFactsApiProviderTests
{
    // Test data for parameterized tests
    public static TheoryData<string, bool> ResponseStatusData => new()
    {
        { @"{""status"":1,""code"":""3017620422003"",""product"":{""product_name"":""Nutella""}}", true },
        { @"{""status"":0,""status_verbose"":""product not found""}", false },
        { @"{""status"":1,""code"":""123456789"",""product"":null}", false }
    };

    public static TheoryData<string, string, string, string> ProductDataMappingData => new()
    {
        // barcode, product_name, brands, expected_name
        { "3017620422003", "Nutella", "Ferrero", "Nutella" },
        { "123456", "", "TestBrand", "Unknown Product" },
        { "789012", "Test Product", "", "Test Product" }
    };

    public static TheoryData<string, string, string, string, string> DescriptionBuildingData => new()
    {
        // brands, quantity, categories, imageUrl, expectedDescriptionContains
        { "Ferrero", "400g", "Spreads", "http://test.com/img.jpg", "Brand: Ferrero" },
        { "", "500ml", "Beverages", "http://test.com/img2.jpg", "Quantity: 500ml" },
        { "TestBrand", "", "", "", "Brand: TestBrand" },
        { "", "", "Snacks", "", "Categories: Snacks" }
    };

    [Theory]
    [MemberData(nameof(ResponseStatusData))]
    public async Task Translate_HandlesVariousResponseStatuses(string jsonResponse, bool shouldReturnProduct)
    {
        // Arrange
        var provider = CreateProviderFromJson();
        var httpResponse = CreateHttpResponseMessage(jsonResponse);

        // Act
        var result = await provider.Translate(httpResponse);

        // Assert
        if (shouldReturnProduct)
        {
            Assert.NotNull(result);
            Assert.IsType<BarcodeView>(result);
        }
        else
        {
            Assert.Null(result);
        }
    }

    [Theory]
    [MemberData(nameof(ProductDataMappingData))]
    public async Task Translate_MapsProductNameCorrectly(string barcode, string productName, string brands, string expectedName)
    {
        // Arrange
        var provider = CreateProviderFromJson();
        var jsonResponse = CreateProductJson(barcode, productName, brands);
        var httpResponse = CreateHttpResponseMessage(jsonResponse);

        // Act
        var result = await provider.Translate(httpResponse);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedName, result.Name);
        Assert.Equal(barcode, result.Code);
    }

    [Theory]
    [MemberData(nameof(DescriptionBuildingData))]
    public async Task Translate_BuildsDescriptionFromAvailableFields(
        string brands, string quantity, string categories, string imageUrl, string expectedDescriptionContains)
    {
        // Arrange
        var provider = CreateProviderFromJson();
        var product = new
        {
            status = 1,
            code = "123456",
            product = new
            {
                product_name = "Test Product",
                brands = brands,
                quantity = quantity,
                categories = categories,
                image_url = imageUrl
            }
        };
        var jsonResponse = JsonSerializer.Serialize(product);
        var httpResponse = CreateHttpResponseMessage(jsonResponse);

        // Act
        var result = await provider.Translate(httpResponse);

        // Assert
        Assert.NotNull(result);
        Assert.Contains(expectedDescriptionContains, result.Description);
    }

    [Fact]
    public async Task Translate_HandlesProductNotFound()
    {
        // Arrange
        var provider = CreateProviderFromJson();
        var jsonResponse = @"{""status"":0,""status_verbose"":""product not found""}";
        var httpResponse = CreateHttpResponseMessage(jsonResponse);

        // Act
        var result = await provider.Translate(httpResponse);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Translate_HandlesNullProduct()
    {
        // Arrange
        var provider = CreateProviderFromJson();
        var jsonResponse = @"{""status"":1,""code"":""123"",""product"":null}";
        var httpResponse = CreateHttpResponseMessage(jsonResponse);

        // Act
        var result = await provider.Translate(httpResponse);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Translate_UsesImageFrontUrlOverImageUrl()
    {
        // Arrange
        var provider = CreateProviderFromJson();
        var product = new
        {
            status = 1,
            code = "123456",
            product = new
            {
                product_name = "Test Product",
                image_url = "http://test.com/image.jpg",
                image_front_url = "http://test.com/front.jpg"
            }
        };
        var jsonResponse = JsonSerializer.Serialize(product);
        var httpResponse = CreateHttpResponseMessage(jsonResponse);

        // Act
        var result = await provider.Translate(httpResponse);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.ImageAsBase64);
        // The URL should be encoded
        Assert.Contains("front.jpg", result.ImageAsBase64);
    }

    [Fact]
    public async Task GetHttpClient_ConfiguresUserAgent()
    {
        // Arrange
        var provider = CreateProviderFromJson();

        // Act
        var client = await provider.GetHttpClient();

        // Assert
        Assert.NotNull(client);
        Assert.Equal(new Uri("https://world.openfoodfacts.org"), client.BaseAddress);
        Assert.True(client.DefaultRequestHeaders.Contains("User-Agent"));
        var userAgent = client.DefaultRequestHeaders.GetValues("User-Agent").FirstOrDefault();
        Assert.Contains("Bardcode", userAgent);
    }

    [Fact]
    public async Task IsOverRates_ReturnsFalse()
    {
        // Arrange
        var provider = CreateProviderFromJson();

        // Act
        var result = await provider.IsOverRates();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task UpdateRates_CompletesSuccessfully()
    {
        // Arrange
        var provider = CreateProviderFromJson();

        // Act & Assert (should not throw)
        await provider.UpdateRates();
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, true)]
    [InlineData(HttpStatusCode.NotFound, false)]
    [InlineData(HttpStatusCode.InternalServerError, false)]
    [InlineData(HttpStatusCode.BadRequest, false)]
    public async Task IsResponseKosher_ValidatesHttpStatusCode(HttpStatusCode statusCode, bool expected)
    {
        // Arrange
        var provider = CreateProviderFromJson();
        var response = new HttpResponseMessage(statusCode);

        // Act
        var result = await provider.IsResponseKosher(response);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("EAN-13", true)]
    [InlineData("EAN-8", true)]
    [InlineData("UPC-A", true)]
    [InlineData("UPC-E", true)]
    [InlineData("CODE128", false)]
    [InlineData("QR_CODE", false)]
    [InlineData("PDF417", false)]
    public void IsBarcodeTypeAllowed_ValidatesAllowedTypes(string barcodeType, bool expected)
    {
        // Arrange
        var provider = CreateProviderFromJson();

        // Act
        var result = provider.IsBarcodeTypeAllowed(barcodeType);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task Translate_HandlesEmptyDescriptionFields()
    {
        // Arrange
        var provider = CreateProviderFromJson();
        var product = new
        {
            status = 1,
            code = "123456",
            product = new
            {
                product_name = "Test Product",
                brands = "",
                quantity = "",
                categories = ""
            }
        };
        var jsonResponse = JsonSerializer.Serialize(product);
        var httpResponse = CreateHttpResponseMessage(jsonResponse);

        // Act
        var result = await provider.Translate(httpResponse);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("No description available", result.Description);
    }

    [Fact]
    public async Task Translate_SetsImageTypeToJpg()
    {
        // Arrange
        var provider = CreateProviderFromJson();
        var product = new
        {
            status = 1,
            code = "123456",
            product = new
            {
                product_name = "Test Product",
                image_url = "http://test.com/image.jpg"
            }
        };
        var jsonResponse = JsonSerializer.Serialize(product);
        var httpResponse = CreateHttpResponseMessage(jsonResponse);

        // Act
        var result = await provider.Translate(httpResponse);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("jpg", result.ImageType);
    }

    [Fact]
    public void DeserializesFromConfiguration()
    {
        // Arrange
        var configJson = @"{
            ""$type"": ""OpenFoodFactsApiProvider"",
            ""url"": ""https://world.openfoodfacts.org"",
            ""path"": ""api/v2/product/{barcode}.json"",
            ""key"": """",
            ""allowedBarcodeTypes"": [ ""EAN-13"", ""EAN-8"", ""UPC-A"", ""UPC-E"" ]
        }";

        // Act
        var provider = JsonSerializer.Deserialize<ApiProviderConfiguration>(configJson);

        // Assert
        Assert.NotNull(provider);
        Assert.IsType<OpenFoodFactsApiProvider>(provider);
        Assert.Equal("https://world.openfoodfacts.org", provider.Url);
        Assert.Equal("api/v2/product/{barcode}.json", provider.Path);
    }

    // Helper methods
    private OpenFoodFactsApiProvider CreateProviderFromJson()
    {
        var configJson = @"{
            ""$type"": ""OpenFoodFactsApiProvider"",
            ""url"": ""https://world.openfoodfacts.org"",
            ""path"": ""api/v2/product/{barcode}.json"",
            ""key"": """",
            ""allowedBarcodeTypes"": [ ""EAN-13"", ""EAN-8"", ""UPC-A"", ""UPC-E"" ]
        }";
        
        return JsonSerializer.Deserialize<OpenFoodFactsApiProvider>(configJson)!;
    }

    private HttpResponseMessage CreateHttpResponseMessage(string content)
    {
        return new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json")
        };
    }

    private string CreateProductJson(string barcode, string productName, string brands)
    {
        var product = new
        {
            status = 1,
            code = barcode,
            product = new
            {
                product_name = productName,
                brands = brands,
                generic_name = string.IsNullOrEmpty(productName) ? "Generic Name" : ""
            }
        };
        return JsonSerializer.Serialize(product);
    }
}
