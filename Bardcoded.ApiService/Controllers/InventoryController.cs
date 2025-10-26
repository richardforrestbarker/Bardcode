using Bardcoded.ApiService.Data;
using Bardcoded.ApiService.Data.Store;
using Bardcoded.Data.Messages;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace Bardcoded.ApiService.Controllers
{
    [Route("api/inventory")]
    [ApiController]
    [Produces("application/json")]
    public class InventoryController : ControllerBase
    {
        private readonly BarcodeDataContext _context;

        public InventoryController(BarcodeDataContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Gets all inventory items for a specific product.
        /// </summary>
        /// <param name="productId">The product ID</param>
        /// <returns>List of inventory items</returns>
        [HttpGet("by-product/{productId}")]
        [ProducesResponseType(typeof(List<InventoryResponse>), 200)]
        public async Task<IActionResult> GetByProduct(Guid productId)
        {
            var items = await _context.InventoryItems
                .Where(i => i.ProductId == productId)
                .Select(i => new InventoryResponse
                {
                    Id = i.Id,
                    ProductId = i.ProductId,
                    Barcode = i.Barcode,
                    BarcodeType = i.BarcodeType,
                    Quantity = i.Quantity,
                    ReservedQuantity = i.ReservedQuantity,
                    Location = i.Location,
                    LastUpdatedAt = i.LastUpdatedAt,
                    LastUpdatedBy = i.LastUpdatedBy
                })
                .ToListAsync();

            return Ok(items);
        }

        /// <summary>
        /// Gets an inventory item by barcode and barcode type.
        /// </summary>
        /// <param name="barcode">The barcode value</param>
        /// <param name="barcodeType">The barcode type</param>
        /// <returns>The inventory item</returns>
        [HttpGet("by-barcode/{barcode}")]
        [ProducesResponseType(typeof(InventoryResponse), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetByBarcode(string barcode, [FromQuery] string barcodeType)
        {
            var item = await _context.InventoryItems
                .FirstOrDefaultAsync(i => i.Barcode == barcode && i.BarcodeType == barcodeType);

            if (item == null)
            {
                return NotFound();
            }

            var response = new InventoryResponse
            {
                Id = item.Id,
                ProductId = item.ProductId,
                Barcode = item.Barcode,
                BarcodeType = item.BarcodeType,
                Quantity = item.Quantity,
                ReservedQuantity = item.ReservedQuantity,
                Location = item.Location,
                LastUpdatedAt = item.LastUpdatedAt,
                LastUpdatedBy = item.LastUpdatedBy
            };

            return Ok(response);
        }

        /// <summary>
        /// Creates a new inventory item.
        /// </summary>
        /// <param name="request">The create request</param>
        /// <returns>The created inventory item</returns>
        [HttpPost]
        [ProducesResponseType(typeof(InventoryResponse), 201)]
        [ProducesResponseType(typeof(ProblemDetails), 400)]
        [ProducesResponseType(typeof(ProblemDetails), 409)]
        public async Task<IActionResult> Create([FromBody] InventoryCreateRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Check if item already exists
            var existing = await _context.InventoryItems
                .FirstOrDefaultAsync(i => i.Barcode == request.Barcode && i.BarcodeType == request.BarcodeType);

            if (existing != null)
            {
                return Conflict(new ProblemDetails
                {
                    Status = (int)HttpStatusCode.Conflict,
                    Title = "Inventory Item Already Exists",
                    Detail = $"An inventory item with barcode '{request.Barcode}' and type '{request.BarcodeType}' already exists."
                });
            }

            var item = new InventoryItem
            {
                Id = Guid.NewGuid(),
                ProductId = request.ProductId,
                Barcode = request.Barcode,
                BarcodeType = request.BarcodeType,
                Quantity = request.Quantity,
                ReservedQuantity = 0,
                Location = request.Location,
                LastUpdatedAt = DateTimeOffset.UtcNow,
                LastUpdatedBy = "system" // TODO: Get from authenticated user
            };

            _context.InventoryItems.Add(item);
            await _context.SaveChangesAsync();

            var response = new InventoryResponse
            {
                Id = item.Id,
                ProductId = item.ProductId,
                Barcode = item.Barcode,
                BarcodeType = item.BarcodeType,
                Quantity = item.Quantity,
                ReservedQuantity = item.ReservedQuantity,
                Location = item.Location,
                LastUpdatedAt = item.LastUpdatedAt,
                LastUpdatedBy = item.LastUpdatedBy
            };

            return CreatedAtAction(nameof(GetByBarcode), new { barcode = item.Barcode, barcodeType = item.BarcodeType }, response);
        }

        /// <summary>
        /// Updates the quantity of an inventory item.
        /// </summary>
        /// <param name="id">The inventory item ID</param>
        /// <param name="request">The update request</param>
        /// <returns>The updated inventory item</returns>
        [HttpPut("{id}/count")]
        [ProducesResponseType(typeof(InventoryResponse), 200)]
        [ProducesResponseType(typeof(ProblemDetails), 400)]
        [ProducesResponseType(typeof(ProblemDetails), 404)]
        [ProducesResponseType(typeof(ProblemDetails), 409)]
        public async Task<IActionResult> UpdateCount(Guid id, [FromBody] InventoryUpdateCountRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Validate that exactly one of SetCount or Delta is specified
            if (request.SetCount.HasValue && request.Delta.HasValue)
            {
                return BadRequest(new ProblemDetails
                {
                    Status = (int)HttpStatusCode.BadRequest,
                    Title = "Invalid Request",
                    Detail = "Only one of 'setCount' or 'delta' can be specified, not both."
                });
            }

            if (!request.SetCount.HasValue && !request.Delta.HasValue)
            {
                return BadRequest(new ProblemDetails
                {
                    Status = (int)HttpStatusCode.BadRequest,
                    Title = "Invalid Request",
                    Detail = "Either 'setCount' or 'delta' must be specified."
                });
            }

            var item = await _context.InventoryItems.FindAsync(id);
            if (item == null)
            {
                return NotFound(new ProblemDetails
                {
                    Status = (int)HttpStatusCode.NotFound,
                    Title = "Inventory Item Not Found",
                    Detail = $"Inventory item with ID '{id}' was not found."
                });
            }

            try
            {
                if (request.SetCount.HasValue)
                {
                    item.Quantity = request.SetCount.Value;
                }
                else if (request.Delta.HasValue)
                {
                    var newQuantity = item.Quantity + request.Delta.Value;
                    if (newQuantity < 0)
                    {
                        return BadRequest(new ProblemDetails
                        {
                            Status = (int)HttpStatusCode.BadRequest,
                            Title = "Invalid Quantity",
                            Detail = $"The resulting quantity ({newQuantity}) would be negative."
                        });
                    }
                    item.Quantity = newQuantity;
                }

                item.LastUpdatedAt = DateTimeOffset.UtcNow;
                item.LastUpdatedBy = "system"; // TODO: Get from authenticated user

                await _context.SaveChangesAsync();

                var response = new InventoryResponse
                {
                    Id = item.Id,
                    ProductId = item.ProductId,
                    Barcode = item.Barcode,
                    BarcodeType = item.BarcodeType,
                    Quantity = item.Quantity,
                    ReservedQuantity = item.ReservedQuantity,
                    Location = item.Location,
                    LastUpdatedAt = item.LastUpdatedAt,
                    LastUpdatedBy = item.LastUpdatedBy
                };

                return Ok(response);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new ProblemDetails
                {
                    Status = (int)HttpStatusCode.Conflict,
                    Title = "Concurrency Conflict",
                    Detail = "The inventory item was modified by another user. Please refresh and try again."
                });
            }
        }

        /// <summary>
        /// Deletes an inventory item.
        /// </summary>
        /// <param name="id">The inventory item ID</param>
        /// <returns>No content on success</returns>
        [HttpDelete("{id}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> Delete(Guid id)
        {
            var item = await _context.InventoryItems.FindAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            _context.InventoryItems.Remove(item);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
