using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Bardcoded.Data.Messages
{
    /// <summary>
    /// Represents a normalized bounding box in model coordinate space (0-1000 scale)
    /// </summary>
    public class BoundingBox
    {
        [JsonPropertyName("x0")]
        public int X0 { get; set; }

        [JsonPropertyName("y0")]
        public int Y0 { get; set; }

        [JsonPropertyName("x1")]
        public int X1 { get; set; }

        [JsonPropertyName("y1")]
        public int Y1 { get; set; }
    }

    /// <summary>
    /// Represents a field extracted from a receipt with confidence and bounding box
    /// </summary>
    public class ExtractedField
    {
        [JsonPropertyName("value")]
        public string? Value { get; set; }

        [JsonPropertyName("confidence")]
        public double Confidence { get; set; }

        [JsonPropertyName("box")]
        public BoundingBox? Box { get; set; }
    }

    /// <summary>
    /// Represents a single word from OCR with its bounding box and confidence
    /// </summary>
    public class OcrWord
    {
        [JsonPropertyName("text")]
        public required string Text { get; set; }

        [JsonPropertyName("box")]
        public required BoundingBox Box { get; set; }

        [JsonPropertyName("confidence")]
        public double Confidence { get; set; }
    }

    /// <summary>
    /// Represents a line item from a receipt
    /// </summary>
    public class LineItem
    {
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("quantity")]
        public decimal? Quantity { get; set; }

        [JsonPropertyName("unit_price")]
        public decimal? UnitPrice { get; set; }

        [JsonPropertyName("line_total")]
        public decimal? LineTotal { get; set; }

        [JsonPropertyName("box")]
        public BoundingBox? Box { get; set; }

        [JsonPropertyName("confidence")]
        public double Confidence { get; set; }
    }

    /// <summary>
    /// Represents a single page of a receipt with OCR results
    /// </summary>
    public class ReceiptPage
    {
        [JsonPropertyName("page_number")]
        public int PageNumber { get; set; }

        [JsonPropertyName("raw_ocr_text")]
        public string? RawOcrText { get; set; }

        [JsonPropertyName("words")]
        public List<OcrWord> Words { get; set; } = new();
    }

    /// <summary>
    /// Complete receipt extraction result
    /// </summary>
    public class ReceiptView
    {
        [JsonPropertyName("job_id")]
        public required string JobId { get; set; }

        [JsonPropertyName("pages")]
        public List<ReceiptPage> Pages { get; set; } = new();

        [JsonPropertyName("vendor_name")]
        public ExtractedField? VendorName { get; set; }

        [JsonPropertyName("merchant_address")]
        public ExtractedField? MerchantAddress { get; set; }

        [JsonPropertyName("date")]
        public ExtractedField? Date { get; set; }

        [JsonPropertyName("total_amount")]
        public ExtractedField? TotalAmount { get; set; }

        [JsonPropertyName("subtotal")]
        public ExtractedField? Subtotal { get; set; }

        [JsonPropertyName("tax_amount")]
        public ExtractedField? TaxAmount { get; set; }

        [JsonPropertyName("currency")]
        public ExtractedField? Currency { get; set; }

        [JsonPropertyName("line_items")]
        public List<LineItem> LineItems { get; set; } = new();

        [JsonPropertyName("status")]
        public string Status { get; set; } = "pending";

        [JsonPropertyName("error_message")]
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Request to upload receipt images for processing
    /// </summary>
    public class ReceiptUploadRequest
    {
        [Description("Optional merchant identifier for tracking")]
        [JsonPropertyName("merchant_id")]
        public string? MerchantId { get; set; }

        [Description("Optional timezone for date parsing")]
        [JsonPropertyName("timezone")]
        public string? Timezone { get; set; }

        [Description("Optional user identifier for tracking")]
        [JsonPropertyName("user_id")]
        public string? UserId { get; set; }

        // Note: Images will be uploaded as multipart/form-data, not JSON
    }

    /// <summary>
    /// Response for receipt upload
    /// </summary>
    public class ReceiptUploadResponse
    {
        [JsonPropertyName("job_id")]
        public required string JobId { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = "processing";

        [JsonPropertyName("status_url")]
        public string? StatusUrl { get; set; }

        [JsonPropertyName("result_url")]
        public string? ResultUrl { get; set; }
    }

    /// <summary>
    /// Response for receipt processing status
    /// </summary>
    public class ReceiptStatusResponse
    {
        [JsonPropertyName("job_id")]
        public required string JobId { get; set; }

        [JsonPropertyName("status")]
        public required string Status { get; set; } // "processing", "done", "failed"

        [JsonPropertyName("progress")]
        public int Progress { get; set; } // 0-100

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }
}
