using Bardcoded.Data.Messages;

namespace Bardcoded.Data.Ocr.Messages
{
    /// <summary>
    /// Result from OCR.
    /// </summary>
    public class OcrResult
    {
        public required string JobId { get; set; }
        public required string Status { get; set; }
        public List<OcrWord> Words { get; set; } = new();
        public string? RawOcrText { get; set; }
        public int ImageWidth { get; set; }
        public int ImageHeight { get; set; }
        public string? Error { get; set; }
    }
}
