using Bardcoded.Data.Messages;
using Xunit;

namespace Bardcoded.Tests;

[Trait("unit", "BarcodeView")]
public class BarcodeViewTests
{
    [Fact]
    public void Create_WithProviderType_SetsProviderType()
    {
        // Arrange
        var code = "123456789";
        var name = "Test Product";
        var description = "Test Description";
        var imageBase64 = "base64data";
        var imageType = "png";
        var providerType = "OpenFoodFacts";

        // Act
        var result = BarcodeView.Create(code, name, description, imageBase64, imageType, providerType);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(code, result.Code);
        Assert.Equal(name, result.Name);
        Assert.Equal(description, result.Description);
        Assert.Equal(imageBase64, result.ImageAsBase64);
        Assert.Equal(imageType, result.ImageType);
        Assert.Equal(providerType, result.ProviderType);
    }

    [Fact]
    public void Create_WithoutProviderType_SetsNullProviderType()
    {
        // Arrange
        var code = "123456789";
        var name = "Test Product";
        var description = "Test Description";
        var imageBase64 = "base64data";
        var imageType = "png";

        // Act
        var result = BarcodeView.Create(code, name, description, imageBase64, imageType);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(code, result.Code);
        Assert.Equal(name, result.Name);
        Assert.Equal(description, result.Description);
        Assert.Equal(imageBase64, result.ImageAsBase64);
        Assert.Equal(imageType, result.ImageType);
        Assert.Null(result.ProviderType);
    }

    [Fact]
    public void Create_WithNullProviderType_SetsNullProviderType()
    {
        // Arrange
        var code = "123456789";
        var name = "Test Product";
        var description = "Test Description";
        var imageBase64 = "base64data";
        var imageType = "png";
        string? providerType = null;

        // Act
        var result = BarcodeView.Create(code, name, description, imageBase64, imageType, providerType);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(code, result.Code);
        Assert.Null(result.ProviderType);
    }

    [Fact]
    public void ProviderType_CanBeSetDirectly()
    {
        // Arrange
        var view = BarcodeView.Create("123", "Name", "Desc", "img", "png");
        var providerType = "UpcDatabase";

        // Act
        view.ProviderType = providerType;

        // Assert
        Assert.Equal(providerType, view.ProviderType);
    }
}
