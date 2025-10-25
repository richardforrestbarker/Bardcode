namespace Bardcoded.Wasm.Components.Barcode
{
    public class BarcodeResult
    {
        public string Text { get; set; }

        public String BarcodeFormat { get; set; }

        public IDictionary<String, object> ResultMetadata { get; set; }
    }
}
