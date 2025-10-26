using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Bardcoded.Data.Messages
{
    public class InventoryResponse
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [JsonPropertyName("productId")]
        public Guid? ProductId { get; set; }

        [JsonPropertyName("barcode")]
        public string Barcode { get; set; }

        [JsonPropertyName("barcodeType")]
        public string BarcodeType { get; set; }

        [JsonPropertyName("quantity")]
        public int Quantity { get; set; }

        [JsonPropertyName("reservedQuantity")]
        public int ReservedQuantity { get; set; }

        [JsonPropertyName("location")]
        public string? Location { get; set; }

        [JsonPropertyName("lastUpdatedAt")]
        public DateTimeOffset LastUpdatedAt { get; set; }

        [JsonPropertyName("lastUpdatedBy")]
        public string LastUpdatedBy { get; set; }
    }

    public class InventoryCreateRequest
    {
        [Description("The product ID this inventory is associated with.")]
        [JsonPropertyName("productId")]
        public Guid? ProductId { get; set; }

        [Description("The barcode value.")]
        [Required(ErrorMessage = $"{nameof(Barcode)} is required.")]
        [StringLength(100, ErrorMessage = $"{nameof(Barcode)} can't be longer than 100 characters.")]
        [JsonPropertyName("barcode")]
        public string Barcode { get; set; }

        [Description("The barcode type (e.g., EAN13, UPC, CODE128).")]
        [Required(ErrorMessage = $"{nameof(BarcodeType)} is required.")]
        [StringLength(50, ErrorMessage = $"{nameof(BarcodeType)} can't be longer than 50 characters.")]
        [JsonPropertyName("barcodeType")]
        public string BarcodeType { get; set; }

        [Description("The initial quantity.")]
        [Range(0, int.MaxValue, ErrorMessage = $"{nameof(Quantity)} must be non-negative.")]
        [JsonPropertyName("quantity")]
        public int Quantity { get; set; }

        [Description("The storage location.")]
        [StringLength(200, ErrorMessage = $"{nameof(Location)} can't be longer than 200 characters.")]
        [JsonPropertyName("location")]
        public string? Location { get; set; }
    }

    public class InventoryUpdateCountRequest
    {
        [Description("The absolute count to set (use this OR delta, not both).")]
        [Range(0, int.MaxValue, ErrorMessage = $"{nameof(SetCount)} must be non-negative.")]
        [JsonPropertyName("setCount")]
        public int? SetCount { get; set; }

        [Description("The delta to add (positive) or subtract (negative) from current count.")]
        [JsonPropertyName("delta")]
        public int? Delta { get; set; }
    }
}
