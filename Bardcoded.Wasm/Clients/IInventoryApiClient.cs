using Bardcoded.Data.Messages;

namespace Bardcoded.Wasm.Clients
{
    public interface IInventoryApiClient
    {
        Task<List<InventoryResponse>> GetByProductAsync(Guid productId);
        Task<InventoryResponse?> GetByBarcodeAsync(string barcode, string barcodeType);
        Task<InventoryResponse> CreateAsync(InventoryCreateRequest request);
        Task<InventoryResponse> UpdateCountAsync(Guid id, InventoryUpdateCountRequest request);
        Task DeleteAsync(Guid id);
    }
}
