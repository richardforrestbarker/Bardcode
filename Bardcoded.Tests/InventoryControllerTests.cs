using Bardcoded.ApiService.Controllers;
using Bardcoded.ApiService.Data;
using Bardcoded.ApiService.Data.Store;
using Bardcoded.Data.Messages;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bardcoded.Tests;

[Trait("unit", "InventoryController")]
public class InventoryControllerTests
{
    private BarcodeDataContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<BarcodeDataContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new BarcodeDataContext(options);
    }

    [Fact]
    public async Task GetByProduct_ReturnsEmptyList_WhenNoItemsExist()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var controller = new InventoryController(context);
        var productId = Guid.NewGuid();

        // Act
        var result = await controller.GetByProduct(productId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var items = Assert.IsType<List<InventoryResponse>>(okResult.Value);
        Assert.Empty(items);
    }

    [Fact]
    public async Task GetByProduct_ReturnsItems_WhenItemsExist()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var productId = Guid.NewGuid();
        var item1 = new InventoryItem
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            Barcode = "123456789",
            BarcodeType = "EAN13",
            Quantity = 10,
            ReservedQuantity = 0,
            LastUpdatedAt = DateTimeOffset.UtcNow,
            LastUpdatedBy = "test"
        };
        var item2 = new InventoryItem
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            Barcode = "987654321",
            BarcodeType = "UPC",
            Quantity = 5,
            ReservedQuantity = 0,
            LastUpdatedAt = DateTimeOffset.UtcNow,
            LastUpdatedBy = "test"
        };
        context.InventoryItems.AddRange(item1, item2);
        await context.SaveChangesAsync();

        var controller = new InventoryController(context);

        // Act
        var result = await controller.GetByProduct(productId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var items = Assert.IsType<List<InventoryResponse>>(okResult.Value);
        Assert.Equal(2, items.Count);
    }

    [Fact]
    public async Task GetByBarcode_ReturnsNotFound_WhenItemDoesNotExist()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var controller = new InventoryController(context);

        // Act
        var result = await controller.GetByBarcode("123456789", "EAN13");

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetByBarcode_ReturnsItem_WhenItemExists()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var item = new InventoryItem
        {
            Id = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            Barcode = "123456789",
            BarcodeType = "EAN13",
            Quantity = 10,
            ReservedQuantity = 0,
            LastUpdatedAt = DateTimeOffset.UtcNow,
            LastUpdatedBy = "test"
        };
        context.InventoryItems.Add(item);
        await context.SaveChangesAsync();

        var controller = new InventoryController(context);

        // Act
        var result = await controller.GetByBarcode("123456789", "EAN13");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<InventoryResponse>(okResult.Value);
        Assert.Equal("123456789", response.Barcode);
        Assert.Equal("EAN13", response.BarcodeType);
        Assert.Equal(10, response.Quantity);
    }

    [Fact]
    public async Task Create_ReturnsCreatedItem_WhenRequestIsValid()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var controller = new InventoryController(context);
        var request = new InventoryCreateRequest
        {
            ProductId = Guid.NewGuid(),
            Barcode = "123456789",
            BarcodeType = "EAN13",
            Quantity = 10,
            Location = "Shelf A"
        };

        // Act
        var result = await controller.Create(request);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        var response = Assert.IsType<InventoryResponse>(createdResult.Value);
        Assert.Equal("123456789", response.Barcode);
        Assert.Equal("EAN13", response.BarcodeType);
        Assert.Equal(10, response.Quantity);
        Assert.Equal("Shelf A", response.Location);
    }

    [Fact]
    public async Task Create_ReturnsConflict_WhenItemAlreadyExists()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var existingItem = new InventoryItem
        {
            Id = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            Barcode = "123456789",
            BarcodeType = "EAN13",
            Quantity = 5,
            ReservedQuantity = 0,
            LastUpdatedAt = DateTimeOffset.UtcNow,
            LastUpdatedBy = "test"
        };
        context.InventoryItems.Add(existingItem);
        await context.SaveChangesAsync();

        var controller = new InventoryController(context);
        var request = new InventoryCreateRequest
        {
            ProductId = Guid.NewGuid(),
            Barcode = "123456789",
            BarcodeType = "EAN13",
            Quantity = 10
        };

        // Act
        var result = await controller.Create(request);

        // Assert
        var conflictResult = Assert.IsType<ConflictObjectResult>(result);
        Assert.IsType<ProblemDetails>(conflictResult.Value);
    }

    [Fact]
    public async Task UpdateCount_WithSetCount_UpdatesQuantity()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var item = new InventoryItem
        {
            Id = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            Barcode = "123456789",
            BarcodeType = "EAN13",
            Quantity = 10,
            ReservedQuantity = 0,
            LastUpdatedAt = DateTimeOffset.UtcNow,
            LastUpdatedBy = "test"
        };
        context.InventoryItems.Add(item);
        await context.SaveChangesAsync();

        var controller = new InventoryController(context);
        var request = new InventoryUpdateCountRequest
        {
            SetCount = 20
        };

        // Act
        var result = await controller.UpdateCount(item.Id, request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<InventoryResponse>(okResult.Value);
        Assert.Equal(20, response.Quantity);
    }

    [Fact]
    public async Task UpdateCount_WithDelta_UpdatesQuantity()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var item = new InventoryItem
        {
            Id = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            Barcode = "123456789",
            BarcodeType = "EAN13",
            Quantity = 10,
            ReservedQuantity = 0,
            LastUpdatedAt = DateTimeOffset.UtcNow,
            LastUpdatedBy = "test"
        };
        context.InventoryItems.Add(item);
        await context.SaveChangesAsync();

        var controller = new InventoryController(context);
        var request = new InventoryUpdateCountRequest
        {
            Delta = 5
        };

        // Act
        var result = await controller.UpdateCount(item.Id, request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<InventoryResponse>(okResult.Value);
        Assert.Equal(15, response.Quantity);
    }

    [Fact]
    public async Task UpdateCount_WithNegativeDelta_ReturnsBadRequest_WhenResultIsNegative()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var item = new InventoryItem
        {
            Id = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            Barcode = "123456789",
            BarcodeType = "EAN13",
            Quantity = 5,
            ReservedQuantity = 0,
            LastUpdatedAt = DateTimeOffset.UtcNow,
            LastUpdatedBy = "test"
        };
        context.InventoryItems.Add(item);
        await context.SaveChangesAsync();

        var controller = new InventoryController(context);
        var request = new InventoryUpdateCountRequest
        {
            Delta = -10
        };

        // Act
        var result = await controller.UpdateCount(item.Id, request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.IsType<ProblemDetails>(badRequestResult.Value);
    }

    [Fact]
    public async Task UpdateCount_ReturnsBadRequest_WhenBothSetCountAndDeltaProvided()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var item = new InventoryItem
        {
            Id = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            Barcode = "123456789",
            BarcodeType = "EAN13",
            Quantity = 10,
            ReservedQuantity = 0,
            LastUpdatedAt = DateTimeOffset.UtcNow,
            LastUpdatedBy = "test"
        };
        context.InventoryItems.Add(item);
        await context.SaveChangesAsync();

        var controller = new InventoryController(context);
        var request = new InventoryUpdateCountRequest
        {
            SetCount = 20,
            Delta = 5
        };

        // Act
        var result = await controller.UpdateCount(item.Id, request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.IsType<ProblemDetails>(badRequestResult.Value);
    }

    [Fact]
    public async Task Delete_ReturnsNoContent_WhenItemExists()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var item = new InventoryItem
        {
            Id = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            Barcode = "123456789",
            BarcodeType = "EAN13",
            Quantity = 10,
            ReservedQuantity = 0,
            LastUpdatedAt = DateTimeOffset.UtcNow,
            LastUpdatedBy = "test"
        };
        context.InventoryItems.Add(item);
        await context.SaveChangesAsync();

        var controller = new InventoryController(context);

        // Act
        var result = await controller.Delete(item.Id);

        // Assert
        Assert.IsType<NoContentResult>(result);
        Assert.Empty(context.InventoryItems);
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenItemDoesNotExist()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var controller = new InventoryController(context);

        // Act
        var result = await controller.Delete(Guid.NewGuid());

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }
}
