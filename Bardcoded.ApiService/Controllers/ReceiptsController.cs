using Bardcoded.Data;
using Bardcoded.Data.Messages;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;

namespace Bardcoded.ApiService.Controllers
{
    /// <summary>
    /// Controller for receipt OCR and processing
    /// </summary>
    [Route("api/receipts")]
    [ApiController]
    [Produces("application/json")]
    public class ReceiptsController : ControllerBase
    {
        private readonly ILogger<ReceiptsController> _logger;
        private readonly IReceiptProcessor _receiptProcessor;
        private readonly OcrConfiguration _ocrConfig;

        public ReceiptsController(
            ILogger<ReceiptsController> logger,
            IReceiptProcessor receiptProcessor,
            OcrConfiguration ocrConfig)
        {
            _logger = logger;
            _receiptProcessor = receiptProcessor;
            _ocrConfig = ocrConfig;
        }

        /// <summary>
        /// Upload receipt images for processing
        /// </summary>
        /// <param name="files">Receipt image files</param>
        /// <param name="merchantId">Optional merchant identifier</param>
        /// <param name="timezone">Optional timezone for date parsing</param>
        /// <param name="userId">Optional user identifier</param>
        /// <returns>Upload response with job ID and status URLs</returns>
        [HttpPost("upload")]
        [ProducesResponseType(typeof(ReceiptUploadResponse), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(413)] // Payload too large
        public async Task<IActionResult> UploadReceipt(
            [FromForm] IFormFileCollection files,
            [FromForm] string? merchantId = null,
            [FromForm] string? timezone = null,
            [FromForm] string? userId = null)
        {
            try
            {
                if (files == null || files.Count == 0)
                {
                    return BadRequest(new { error = "No files uploaded" });
                }

                // Validate file sizes
                foreach (var file in files)
                {
                    if (file.Length > _ocrConfig.MaxFileSize)
                    {
                        return StatusCode(413, new { error = $"File {file.FileName} exceeds maximum size of {_ocrConfig.MaxFileSize} bytes" });
                    }

                    // Validate file type (basic check)
                    var contentType = file.ContentType.ToLowerInvariant();
                    if (!contentType.StartsWith("image/"))
                    {
                        return BadRequest(new { error = $"File {file.FileName} is not an image" });
                    }
                }

                var request = new ReceiptUploadRequest
                {
                    MerchantId = merchantId,
                    Timezone = timezone,
                    UserId = userId
                };

                var jobId = await _receiptProcessor.ProcessReceiptAsync(files, request);

                var response = new ReceiptUploadResponse
                {
                    JobId = jobId,
                    Status = "processing",
                    StatusUrl = Url.Action(nameof(GetStatus), new { jobId }),
                    ResultUrl = Url.Action(nameof(GetResult), new { jobId })
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading receipt");
                return StatusCode(500, new { error = "Internal server error processing receipt" });
            }
        }

        /// <summary>
        /// Get the status of a receipt processing job
        /// </summary>
        /// <param name="jobId">Job identifier</param>
        /// <returns>Current status of the job</returns>
        [HttpGet("status/{jobId}")]
        [ProducesResponseType(typeof(ReceiptStatusResponse), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetStatus(string jobId)
        {
            try
            {
                var status = await _receiptProcessor.GetStatusAsync(jobId);
                
                if (status == null)
                {
                    return NotFound(new { error = $"Job {jobId} not found" });
                }

                return Ok(status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting receipt status for job {JobId}", jobId);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Get the result of a completed receipt processing job
        /// </summary>
        /// <param name="jobId">Job identifier</param>
        /// <returns>Extracted receipt data</returns>
        [HttpGet("result/{jobId}")]
        [ProducesResponseType(typeof(ReceiptView), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(425)] // Too Early - processing not complete
        public async Task<IActionResult> GetResult(string jobId)
        {
            try
            {
                var result = await _receiptProcessor.GetResultAsync(jobId);
                
                if (result == null)
                {
                    return NotFound(new { error = $"Job {jobId} not found" });
                }

                if (result.Status == "processing")
                {
                    return StatusCode(425, new { error = "Receipt is still processing", status = result.Status });
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting receipt result for job {JobId}", jobId);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }
    }

    /// <summary>
    /// Interface for receipt processing service
    /// </summary>
    public interface IReceiptProcessor
    {
        Task<string> ProcessReceiptAsync(IFormFileCollection files, ReceiptUploadRequest request);
        Task<ReceiptStatusResponse?> GetStatusAsync(string jobId);
        Task<ReceiptView?> GetResultAsync(string jobId);
    }

    /// <summary>
    /// In-memory receipt processor implementation
    /// </summary>
    public class ReceiptProcessor : IReceiptProcessor
    {
        private readonly ILogger<ReceiptProcessor> _logger;
        private readonly OcrConfiguration _config;
        private readonly ConcurrentDictionary<string, ReceiptView> _jobs = new();
        private readonly ConcurrentDictionary<string, ReceiptStatusResponse> _statuses = new();

        public ReceiptProcessor(ILogger<ReceiptProcessor> logger, OcrConfiguration config)
        {
            _logger = logger;
            _config = config;
        }

        public async Task<string> ProcessReceiptAsync(IFormFileCollection files, ReceiptUploadRequest request)
        {
            var jobId = Guid.NewGuid().ToString();
            
            // Initialize status
            var status = new ReceiptStatusResponse
            {
                JobId = jobId,
                Status = "processing",
                Progress = 0,
                Message = "Starting receipt processing"
            };
            _statuses[jobId] = status;

            // Save files to temporary storage
            var tempDir = Path.Combine(_config.TempStoragePath, jobId);
            Directory.CreateDirectory(tempDir);

            var filePaths = new List<string>();
            foreach (var file in files)
            {
                var filePath = Path.Combine(tempDir, file.FileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }
                filePaths.Add(filePath);
            }

            // Start background processing
            _ = Task.Run(async () => await ProcessReceiptInBackgroundAsync(jobId, filePaths, request));

            return jobId;
        }

        private async Task ProcessReceiptInBackgroundAsync(string jobId, List<string> filePaths, ReceiptUploadRequest request)
        {
            try
            {
                // Update status
                _statuses[jobId] = new ReceiptStatusResponse
                {
                    JobId = jobId,
                    Status = "processing",
                    Progress = 25,
                    Message = "Running OCR on receipt images"
                };

                // This is a placeholder - the actual OCR processing will be implemented
                // when the Python service is integrated
                await Task.Delay(2000); // Simulate processing

                // Create mock result for now
                var result = new ReceiptView
                {
                    JobId = jobId,
                    Status = "done",
                    Pages = new List<ReceiptPage>
                    {
                        new ReceiptPage
                        {
                            PageNumber = 1,
                            RawOcrText = "Sample receipt text",
                            Words = new List<OcrWord>()
                        }
                    },
                    VendorName = new ExtractedField
                    {
                        Value = "Sample Store",
                        Confidence = 0.95,
                        Box = new BoundingBox { X0 = 100, Y0 = 50, X1 = 300, Y1 = 100 }
                    },
                    TotalAmount = new ExtractedField
                    {
                        Value = "25.99",
                        Confidence = 0.92,
                        Box = new BoundingBox { X0 = 400, Y0 = 500, X1 = 500, Y1 = 550 }
                    }
                };

                _jobs[jobId] = result;
                _statuses[jobId] = new ReceiptStatusResponse
                {
                    JobId = jobId,
                    Status = "done",
                    Progress = 100,
                    Message = "Receipt processing complete"
                };

                _logger.LogInformation("Receipt processing completed for job {JobId}", jobId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing receipt for job {JobId}", jobId);
                
                _statuses[jobId] = new ReceiptStatusResponse
                {
                    JobId = jobId,
                    Status = "failed",
                    Progress = 0,
                    Error = ex.Message
                };

                _jobs[jobId] = new ReceiptView
                {
                    JobId = jobId,
                    Status = "failed",
                    ErrorMessage = ex.Message
                };
            }
        }

        public Task<ReceiptStatusResponse?> GetStatusAsync(string jobId)
        {
            _statuses.TryGetValue(jobId, out var status);
            return Task.FromResult(status);
        }

        public Task<ReceiptView?> GetResultAsync(string jobId)
        {
            _jobs.TryGetValue(jobId, out var result);
            return Task.FromResult(result);
        }
    }
}
