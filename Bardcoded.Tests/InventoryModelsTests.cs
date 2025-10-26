using Bardcoded.Data.Messages;
using System.ComponentModel.DataAnnotations;
using Xunit;

namespace Bardcoded.Tests;

[Trait("unit", "InventoryModels")]
public class InventoryModelsTests
{
    [Fact]
    public void InventoryResponse_CanBeCreated()
    {
        // Arrange & Act
        var response = new InventoryResponse
        {
            Id = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            Barcode = "123456789",
            BarcodeType = "EAN13",
            Quantity = 10,
            ReservedQuantity = 2,
            Location = "Shelf A",
            LastUpdatedAt = DateTimeOffset.UtcNow,
            LastUpdatedBy = "user123"
        };

        // Assert
        Assert.NotEqual(Guid.Empty, response.Id);
        Assert.NotNull(response.ProductId);
        Assert.Equal("123456789", response.Barcode);
        Assert.Equal("EAN13", response.BarcodeType);
        Assert.Equal(10, response.Quantity);
        Assert.Equal(2, response.ReservedQuantity);
        Assert.Equal("Shelf A", response.Location);
        Assert.Equal("user123", response.LastUpdatedBy);
    }

    [Fact]
    public void InventoryCreateRequest_ValidRequest_PassesValidation()
    {
        // Arrange
        var request = new InventoryCreateRequest
        {
            ProductId = Guid.NewGuid(),
            Barcode = "123456789",
            BarcodeType = "EAN13",
            Quantity = 10,
            Location = "Shelf A"
        };

        // Act
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(request, new ValidationContext(request), validationResults, true);

        // Assert
        Assert.True(isValid);
        Assert.Empty(validationResults);
    }

    [Fact]
    public void InventoryCreateRequest_MissingBarcode_FailsValidation()
    {
        // Arrange
        var request = new InventoryCreateRequest
        {
            ProductId = Guid.NewGuid(),
            Barcode = null!,
            BarcodeType = "EAN13",
            Quantity = 10
        };

        // Act
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(request, new ValidationContext(request), validationResults, true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(validationResults, v => v.MemberNames.Contains(nameof(InventoryCreateRequest.Barcode)));
    }

    [Fact]
    public void InventoryCreateRequest_MissingBarcodeType_FailsValidation()
    {
        // Arrange
        var request = new InventoryCreateRequest
        {
            ProductId = Guid.NewGuid(),
            Barcode = "123456789",
            BarcodeType = null!,
            Quantity = 10
        };

        // Act
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(request, new ValidationContext(request), validationResults, true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(validationResults, v => v.MemberNames.Contains(nameof(InventoryCreateRequest.BarcodeType)));
    }

    [Fact]
    public void InventoryCreateRequest_NegativeQuantity_FailsValidation()
    {
        // Arrange
        var request = new InventoryCreateRequest
        {
            ProductId = Guid.NewGuid(),
            Barcode = "123456789",
            BarcodeType = "EAN13",
            Quantity = -5
        };

        // Act
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(request, new ValidationContext(request), validationResults, true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(validationResults, v => v.MemberNames.Contains(nameof(InventoryCreateRequest.Quantity)));
    }

    [Fact]
    public void InventoryUpdateCountRequest_WithSetCount_IsValid()
    {
        // Arrange
        var request = new InventoryUpdateCountRequest
        {
            SetCount = 15
        };

        // Act
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(request, new ValidationContext(request), validationResults, true);

        // Assert
        Assert.True(isValid);
        Assert.Empty(validationResults);
        Assert.NotNull(request.SetCount);
        Assert.Null(request.Delta);
    }

    [Fact]
    public void InventoryUpdateCountRequest_WithDelta_IsValid()
    {
        // Arrange
        var request = new InventoryUpdateCountRequest
        {
            Delta = 5
        };

        // Act
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(request, new ValidationContext(request), validationResults, true);

        // Assert
        Assert.True(isValid);
        Assert.Empty(validationResults);
        Assert.Null(request.SetCount);
        Assert.NotNull(request.Delta);
    }

    [Fact]
    public void InventoryUpdateCountRequest_NegativeSetCount_FailsValidation()
    {
        // Arrange
        var request = new InventoryUpdateCountRequest
        {
            SetCount = -5
        };

        // Act
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(request, new ValidationContext(request), validationResults, true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(validationResults, v => v.MemberNames.Contains(nameof(InventoryUpdateCountRequest.SetCount)));
    }

    [Fact]
    public void InventoryUpdateCountRequest_NegativeDelta_IsValid()
    {
        // Arrange - negative deltas are allowed at model level, business logic handles the validation
        var request = new InventoryUpdateCountRequest
        {
            Delta = -5
        };

        // Act
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(request, new ValidationContext(request), validationResults, true);

        // Assert
        Assert.True(isValid); // Model validation passes, business logic should check if result would be negative
        Assert.Empty(validationResults);
    }
}
