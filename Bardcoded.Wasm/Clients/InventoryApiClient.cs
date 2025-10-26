using Bardcoded.Data.Messages;
using System.Net.Http.Json;
using System.Text.Encodings.Web;

namespace Bardcoded.Wasm.Clients
{
    public class InventoryApiClient : IInventoryApiClient
    {
        private readonly HttpClient _httpClient;

        public InventoryApiClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<List<InventoryResponse>> GetByProductAsync(Guid productId)
        {
            var response = await _httpClient.GetAsync($"api/inventory/by-product/{productId}");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<InventoryResponse>>() ?? new List<InventoryResponse>();
        }

        public async Task<InventoryResponse?> GetByBarcodeAsync(string barcode, string barcodeType)
        {
            var encodedBarcode = UrlEncoder.Default.Encode(barcode);
            var encodedType = UrlEncoder.Default.Encode(barcodeType);
            var response = await _httpClient.GetAsync($"api/inventory/by-barcode/{encodedBarcode}?barcodeType={encodedType}");
            
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<InventoryResponse>();
        }

        public async Task<InventoryResponse> CreateAsync(InventoryCreateRequest request)
        {
            var response = await _httpClient.PostAsJsonAsync("api/inventory", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<InventoryResponse>() 
                ?? throw new InvalidOperationException("Failed to deserialize response");
        }

        public async Task<InventoryResponse> UpdateCountAsync(Guid id, InventoryUpdateCountRequest request)
        {
            var response = await _httpClient.PutAsJsonAsync($"api/inventory/{id}/count", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<InventoryResponse>() 
                ?? throw new InvalidOperationException("Failed to deserialize response");
        }

        public async Task DeleteAsync(Guid id)
        {
            var response = await _httpClient.DeleteAsync($"api/inventory/{id}");
            response.EnsureSuccessStatusCode();
        }
    }
}
