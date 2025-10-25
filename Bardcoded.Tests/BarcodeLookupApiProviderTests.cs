using Bardcoded.ApiService.Providers;
using Bardcoded.Data.Api;
using Bardcoded.Data.Messages;
using System.Net;
using System.Text.Json;
using Xunit;

namespace Bardcoded.Tests;
[Trait("unit", "ApiClient")]
public class BarcodeLookupApiProviderTests
{
    public BarcodeLookupApiProviderTests(){}

    // Test data for parameterized tests
    public static TheoryData<string, bool> ResponseTypeData => new()
    {
        // Valid product response
        { @"{""products"":[{""barcode_number"":""012345678905"",""title"":""Test Product"",""brand"":""TestBrand""}]}", true },
        // Error response
        { @"{""error"":""404"",""message"":""Product not found""}", false },
        // Empty products array
        { @"{""products"":[]}", false },
        // Response without products
        { @"{""some_field"":""value""}", false }
    };

    public static TheoryData<string, string, string, string> ProductDataMappingData => new()
    {
        // barcode, title, productName, expectedName
        { "012345678905", "Primary Title", "", "Primary Title" },
        { "123456789012", "", "Product Name", "Product Name" },
        { "098765432109", "", "", "Unknown Product" },
        { "111111111111", "Title", "Name", "Title" } // Title takes precedence
    };

    public static TheoryData<string, string, string, string, string, string> DescriptionBuildingData => new()
    {
        // description, brand, manufacturer, category, size, expectedContains
        { "Product description", "BrandName", "", "", "", "Product description" },
        { "", "BrandName", "ManufacturerName", "", "", "Brand: BrandName" },
        { "", "", "ManufacturerName", "Electronics", "", "Manufacturer: ManufacturerName" },
        { "", "", "", "Electronics", "Large", "Category: Electronics" },
        { "", "", "", "", "10oz", "Size: 10oz" },
        { "Description", "Brand", "Mfg", "Cat", "Size", "Description" }
    };

    [Theory]
    [MemberData(nameof(ResponseTypeData))]
    public async Task Translate_HandlesVariousResponseTypes(string jsonResponse, bool shouldReturnProduct)
    {
        // Arrange
        var provider = CreateProviderFromJson(CreateConfigFromJson());
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
    public async Task Translate_MapsProductNameCorrectly(string barcode, string title, string productName, string expectedName)
    {
        // Arrange
        var provider = CreateProviderFromJson(CreateConfigFromJson());
        var jsonResponse = CreateProductJson(barcode, title, productName, "TestBrand");
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
        string description, string brand, string manufacturer, string category, string size, string expectedContains)
    {
        // Arrange
        var provider = CreateProviderFromJson(CreateConfigFromJson());
        var product = new
        {
            products = new[]
            {
                new
                {
                    barcode_number = "123456",
                    title = "Test Product",
                    description = description,
                    brand = brand,
                    manufacturer = manufacturer,
                    category = category,
                    size = size
                }
            }
        };
        var jsonResponse = JsonSerializer.Serialize(product);
        var httpResponse = CreateHttpResponseMessage(jsonResponse);

        // Act
        var result = await provider.Translate(httpResponse);

        // Assert
        Assert.NotNull(result);
        Assert.Contains(expectedContains, result.Description);
    }

    [Fact]
    public async Task Translate_HandlesErrorResponse()
    {
        // Arrange
        var provider = CreateProviderFromJson(CreateConfigFromJson());
        var jsonResponse = @"{""error"":""404"",""message"":""Product not found""}";
        var httpResponse = CreateHttpResponseMessage(jsonResponse);

        // Act
        var result = await provider.Translate(httpResponse);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Translate_HandlesEmptyProductsArray()
    {
        // Arrange
        var provider = CreateProviderFromJson(CreateConfigFromJson());
        var jsonResponse = @"{""products"":[]}";
        var httpResponse = CreateHttpResponseMessage(jsonResponse);

        // Act
        var result = await provider.Translate(httpResponse);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Translate_HandlesNullProducts()
    {
        // Arrange
        var provider = CreateProviderFromJson(CreateConfigFromJson());
        var jsonResponse = @"{""products"":null}";
        var httpResponse = CreateHttpResponseMessage(jsonResponse);

        // Act
        var result = await provider.Translate(httpResponse);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Translate_UsesFirstProductWhenMultipleReturned()
    {
        // Arrange
        var provider = CreateProviderFromJson(CreateConfigFromJson());
        var jsonResponse = @"{
            ""products"": [
                {""barcode_number"":""123456"",""title"":""First Product""},
                {""barcode_number"":""789012"",""title"":""Second Product""}
            ]
        }";
        var httpResponse = CreateHttpResponseMessage(jsonResponse);

        // Act
        var result = await provider.Translate(httpResponse);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("First Product", result.Name);
        Assert.Equal("123456", result.Code);
    }

    [Fact]
    public async Task Translate_IncludesImageUrl()
    {
        // Arrange
        var provider = CreateProviderFromJson(CreateConfigFromJson());
        var product = new
        {
            products = new[]
            {
                new
                {
                    barcode_number = "123456",
                    title = "Test Product",
                    images = new[] { "http://example.com/image1.jpg", "http://example.com/image2.jpg" }
                }
            }
        };
        var jsonResponse = JsonSerializer.Serialize(product);
        var httpResponse = CreateHttpResponseMessage(jsonResponse);

        // Act
        var result = await provider.Translate(httpResponse);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.ImageAsBase64);
        Assert.Contains("image1.jpg", result.ImageAsBase64);
    }

    [Fact]
    public async Task Translate_HandlesNoImages()
    {
        // Arrange
        var provider = CreateProviderFromJson(CreateConfigFromJson());
        var product = new
        {
            products = new[]
            {
                new
                {
                    barcode_number = "123456",
                    title = "Test Product",
                    images = Array.Empty<string>()
                }
            }
        };
        var jsonResponse = JsonSerializer.Serialize(product);
        var httpResponse = CreateHttpResponseMessage(jsonResponse);

        // Act
        var result = await provider.Translate(httpResponse);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.ImageAsBase64);
    }

    [Fact]
    public async Task Translate_SetsImageTypeToJpg()
    {
        // Arrange
        var provider = CreateProviderFromJson(CreateConfigFromJson());
        var product = new
        {
            products = new[]
            {
                new
                {
                    barcode_number = "123456",
                    title = "Test Product",
                    images = new[] { "http://example.com/image.jpg" }
                }
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
    public async Task Translate_HandlesEmptyDescriptionFields()
    {
        // Arrange
        var provider = CreateProviderFromJson(CreateConfigFromJson());
        var product = new
        {
            products = new[]
            {
                new
                {
                    barcode_number = "123456",
                    title = "Test Product",
                    description = "",
                    brand = "",
                    manufacturer = "",
                    category = "",
                    size = ""
                }
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
    public async Task Translate_IncludesFeaturesInDescription()
    {
        // Arrange
        var provider = CreateProviderFromJson(CreateConfigFromJson());
        var product = new
        {
            products = new[]
            {
                new
                {
                    barcode_number = "123456",
                    title = "Test Product",
                    features = new[] { "Feature 1", "Feature 2", "Feature 3", "Feature 4" }
                }
            }
        };
        var jsonResponse = JsonSerializer.Serialize(product);
        var httpResponse = CreateHttpResponseMessage(jsonResponse);

        // Act
        var result = await provider.Translate(httpResponse);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Features:", result.Description);
        Assert.Contains("Feature 1", result.Description);
        Assert.Contains("Feature 2", result.Description);
        Assert.Contains("Feature 3", result.Description);
        // Should only include first 3 features
        Assert.DoesNotContain("Feature 4", result.Description);
    }

    [Fact]
    public async Task GetHttpClient_ConfiguresBaseAddress()
    {
        // Arrange
        var provider = CreateProviderFromJson(CreateConfigFromJson());

        // Act
        var client = await provider.GetHttpClient();

        // Assert
        Assert.NotNull(client);
        Assert.Equal(new Uri("https://api.barcodelookup.com"), client.BaseAddress);
    }

    [Fact]
    public async Task IsOverRates_ReturnsFalse()
    {
        // Arrange
        var config = CreateConfigFromJson();

        // Act
        var result = await config.IsOverRates();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task UpdateRates_CompletesSuccessfully()
    {
        // Arrange
        var config = CreateConfigFromJson();

        // Act & Assert (should not throw)
        await config.UpdateRates();
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, true)]
    [InlineData(HttpStatusCode.NotFound, false)]
    [InlineData(HttpStatusCode.InternalServerError, false)]
    [InlineData(HttpStatusCode.BadRequest, false)]
    [InlineData(HttpStatusCode.Unauthorized, false)]
    [InlineData(HttpStatusCode.TooManyRequests, false)]
    public async Task IsResponseKosher_ValidatesHttpStatusCode(HttpStatusCode statusCode, bool expected)
    {
        // Arrange
        var provider = CreateProviderFromJson(CreateConfigFromJson());
        var response = new HttpResponseMessage(statusCode);

        // Act
        var result = await provider.IsResponseKosher(response);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("UPC-A", true)]
    [InlineData("UPC-E", true)]
    [InlineData("EAN-13", true)]
    [InlineData("EAN-8", true)]
    [InlineData("ISBN-10", true)]
    [InlineData("ISBN-13", true)]
    [InlineData("CODE128", false)]
    [InlineData("QR_CODE", false)]
    [InlineData("PDF417", false)]
    [InlineData("AZTEC", false)]
    public void IsBarcodeTypeAllowed_ValidatesAllowedTypes(string barcodeType, bool expected)
    {
        // Arrange
        var config = CreateConfigFromJson();

        // Act
        var result = config.IsBarcodeTypeAllowed(barcodeType);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetPathForBarcode_ReplacesBarcodePlaceholder()
    {
        // Arrange
        var provider = CreateProviderFromJson(CreateConfigFromJson());
        var barcode = "012345678905";

        // Act
        var path = provider.GetPathForBarcode(barcode);

        // Assert
        Assert.Contains(barcode, path);
        Assert.DoesNotContain("{barcode}", path);
    }

    [Fact]
    public void GetPathForBarcode_AppendsApiKey()
    {
        // Arrange
        var configJson = @"{
            ""$type"": ""BarcodeLookupApiProvider"",
            ""url"": ""https://api.barcodelookup.com"",
            ""path"": ""v3/products?barcode={barcode}&key="",
            ""key"": ""test-api-key-123"",
            ""allowedBarcodeTypes"": [ ""UPC-A"", ""EAN-13"" ]
        }";
        var provider = JsonSerializer.Deserialize<BarcodeLookupApiProvider>(configJson)!;
        var barcode = "012345678905";

        // Act
        var path = provider.GetPathForBarcode(barcode);

        // Assert
        Assert.Contains("test-api-key-123", path);
    }

    [Fact]
    public void GetPathForBarcode_HandlesEmptyKey()
    {
        // Arrange
        var provider = CreateProviderFromJson(CreateConfigFromJson()); // Has empty key
        var barcode = "012345678905";

        // Act
        var path = provider.GetPathForBarcode(barcode);

        // Assert
        Assert.Contains(barcode, path);
        Assert.Contains("key=", path);
    }

    [Fact]
    public void DeserializesFromConfiguration()
    {
        // Arrange
        var configJson = @"{
            ""$type"": ""BarcodeLookupApiProvider"",
            ""url"": ""https://api.barcodelookup.com"",
            ""path"": ""v3/products?barcode={barcode}&key="",
            ""key"": """",
            ""allowedBarcodeTypes"": [ ""UPC-A"", ""EAN-13"" ]
        }";

        // Act
        var provider = JsonSerializer.Deserialize<ApiProviderConfiguration>(configJson);

        // Assert
        Assert.NotNull(provider);
        Assert.IsType<BarcodeLookupApiProvider>(provider);
        Assert.Equal("https://api.barcodelookup.com", provider.Url);
        Assert.Equal("v3/products?barcode={barcode}&key=", provider.Path);
    }

    [Fact]
    public async Task Translate_HandlesComplexProductWithAllFields()
    {
        // Arrange
        var provider = CreateProviderFromJson(CreateConfigFromJson());
        var product = new
        {
            products = new[]
            {
                new
                {
                    barcode_number = "012345678905",
                    barcode_type = "UPC-A",
                    title = "Premium Product",
                    product_name = "Alternate Name",
                    brand = "TopBrand",
                    manufacturer = "TopManufacturer",
                    category = "Electronics",
                    description = "A high-quality product",
                    size = "Medium",
                    weight = "2 lbs",
                    features = new[] { "Feature A", "Feature B", "Feature C" },
                    images = new[] { "http://example.com/img1.jpg" },
                    color = "Blue",
                    mpn = "MPN123",
                    asin = "ASIN456"
                }
            }
        };
        var jsonResponse = JsonSerializer.Serialize(product);
        var httpResponse = CreateHttpResponseMessage(jsonResponse);

        // Act
        var result = await provider.Translate(httpResponse);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Premium Product", result.Name);
        Assert.Equal("012345678905", result.Code);
        Assert.Contains("A high-quality product", result.Description);
        Assert.Contains("TopBrand", result.Description);
        Assert.NotNull(result.ImageAsBase64);
    }

    [Fact]
    public async Task Translate_PrioritizesLabelOverProductName()
    {
        // Arrange
        var provider = CreateProviderFromJson(CreateConfigFromJson());
        var product = new
        {
            products = new[]
            {
                new
                {
                    barcode_number = "123456",
                    label = "Label Name",
                    product_name = "Product Name"
                }
            }
        };
        var jsonResponse = JsonSerializer.Serialize(product);
        var httpResponse = CreateHttpResponseMessage(jsonResponse);

        // Act
        var result = await provider.Translate(httpResponse);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Label Name", result.Name);
    }

    // Helper methods
    private BarcodeLookupApiProvider CreateProviderFromJson(ApiProviderConfiguration c)
    {
        return new BarcodeLookupApiProvider(c);
    }
    private ApiProviderConfiguration CreateConfigFromJson()
    {
        var configJson = "{\"$type\": \"BarcodeLookupApiProvider\",\"url\": \"https://api.barcodelookup.com\",\"path\": \"v3/products?barcode={barcode}&key=\",\"key\": \"\",\"allowedBarcodeTypes\": [ \"UPC-A\", \"UPC-E\", \"EAN-13\", \"EAN-8\", \"ISBN-10\", \"ISBN-13\" ]}";
        var config = JsonSerializer.Deserialize<ApiProviderConfiguration>(configJson)!;
        config.RateLimit = new RateLimit { TimeSpan = TimeSpan.FromMinutes(1), Limit = 100 };
        config.Rate = new Rate { Count = 0, NextReset = DateTime.UtcNow.AddMinutes(1) };
        return config;
    }

    private HttpResponseMessage CreateHttpResponseMessage(string content)
    {
        return new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json")
        };
    }

    private string CreateProductJson(string barcode, string title, string productName, string brand)
    {
        var product = new
        {
            products = new[]
            {
                new
                {
                    barcode_number = barcode,
                    title = title,
                    product_name = productName,
                    brand = brand
                }
            }
        };
        return JsonSerializer.Serialize(product);
    }
}
