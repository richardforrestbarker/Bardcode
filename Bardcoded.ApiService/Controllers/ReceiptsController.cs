using Bardcoded.Data;
using Bardcoded.Data.Messages;
using DocumentProcessor.Data;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

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
    /// Receipt processor implementation that calls Python CLI
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

            // Start background processing on a separate thread
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
                    Progress = 10,
                    Message = "Preparing to run OCR"
                };

                _logger.LogInformation("Starting OCR processing for job {JobId} with {FileCount} files", jobId, filePaths.Count);

                // Build arguments for Python CLI
                var pythonPath = FindPythonExecutable();
                var cliPath = GetCliPath();
                
                _logger.LogInformation("Python path: {PythonPath}", pythonPath);
                _logger.LogInformation("CLI path: {CliPath}", cliPath);

                // Build command arguments
                var args = new List<string>
                {
                    cliPath,
                    "process"
                };

                // Add image arguments
                foreach (var filePath in filePaths)
                {
                    args.Add("--image");
                    args.Add(filePath);
                }

                // Add job ID
                args.Add("--job-id");
                args.Add(jobId);

                // Add OCR engine
                args.Add("--ocr-engine");
                args.Add(_config.OcrEngine);

                // Add device
                args.Add("--device");
                args.Add(_config.EnableGpu ? "auto" : "cpu");

                // Add model
                args.Add("--model");
                args.Add(_config.ModelNameOrPath);

                _statuses[jobId] = new ReceiptStatusResponse
                {
                    JobId = jobId,
                    Status = "processing",
                    Progress = 25,
                    Message = "Running OCR on receipt images"
                };

                // Run Python CLI process
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = pythonPath,
                    Arguments = string.Join(" ", args.Select(a => a.Contains(' ') ? $"\"{a}\"" : a)),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                _logger.LogInformation("Executing: {FileName} {Arguments}", processStartInfo.FileName, processStartInfo.Arguments);

                using var process = new Process { StartInfo = processStartInfo };
                process.Start();

                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                // Wait for process with timeout (5 minutes)
                var completed = process.WaitForExit(300000);

                var output = await outputTask;
                var error = await errorTask;

                if (!completed)
                {
                    process.Kill();
                    throw new TimeoutException("OCR process timed out after 5 minutes");
                }

                if (!string.IsNullOrWhiteSpace(error))
                {
                    _logger.LogWarning("OCR process stderr: {Error}", error);
                }

                _logger.LogInformation("OCR process completed with exit code {ExitCode}", process.ExitCode);

                if (process.ExitCode != 0)
                {
                    throw new Exception($"OCR process failed with exit code {process.ExitCode}: {error}");
                }

                _statuses[jobId] = new ReceiptStatusResponse
                {
                    JobId = jobId,
                    Status = "processing",
                    Progress = 75,
                    Message = "Parsing OCR results"
                };

                // Parse JSON output
                var result = ParseOcrResult(output, jobId);

                _jobs[jobId] = result;
                _statuses[jobId] = new ReceiptStatusResponse
                {
                    JobId = jobId,
                    Status = "done",
                    Progress = 100,
                    Message = "Receipt processing complete"
                };

                _logger.LogInformation("Receipt processing completed for job {JobId}", jobId);

                // Clean up temp files after successful processing
                try
                {
                    var tempDir = Path.GetDirectoryName(filePaths[0]);
                    if (tempDir != null && Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to clean up temp directory for job {JobId}", jobId);
                }
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

        private string FindPythonExecutable()
        {
            // Try common Python executable names
            var candidates = new[] { "python3", "python", "python3.11", "python3.10" };
            
            foreach (var candidate in candidates)
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = candidate,
                        Arguments = "--version",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    
                    using var process = Process.Start(psi);
                    if (process != null)
                    {
                        process.WaitForExit(5000);
                        if (process.ExitCode == 0)
                        {
                            return candidate;
                        }
                    }
                }
                catch
                {
                    // Continue to next candidate
                }
            }
            
            // Default to python3
            return "python3";
        }

        private string GetCliPath()
        {
            // Get the CLI path from configuration
            var cliPath = _config.PythonServicePath;
            
            // If relative path, resolve from current directory
            if (!Path.IsPathRooted(cliPath))
            {
                cliPath = Path.Combine(Directory.GetCurrentDirectory(), cliPath);
            }
            
            // Normalize path
            cliPath = Path.GetFullPath(cliPath);
            
            if (!File.Exists(cliPath))
            {
                _logger.LogWarning("CLI path not found at {CliPath}, trying alternate locations", cliPath);
                
                // Try alternate locations
                var alternates = new[]
                {
                    Path.Combine(Directory.GetCurrentDirectory(), "Bardcoded.Ocr", "cli.py"),
                    Path.Combine(Directory.GetCurrentDirectory(), "..", "Bardcoded.Ocr", "cli.py"),
                    Path.Combine(AppContext.BaseDirectory, "Bardcoded.Ocr", "cli.py"),
                    Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Bardcoded.Ocr", "cli.py")
                };
                
                foreach (var alt in alternates)
                {
                    var normalized = Path.GetFullPath(alt);
                    if (File.Exists(normalized))
                    {
                        _logger.LogInformation("Found CLI at alternate location: {CliPath}", normalized);
                        return normalized;
                    }
                }
            }
            
            return cliPath;
        }

        private ReceiptView ParseOcrResult(string jsonOutput, string jobId)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var jsonDoc = JsonDocument.Parse(jsonOutput);
                var root = jsonDoc.RootElement;

                var result = new ReceiptView
                {
                    JobId = jobId,
                    Status = root.TryGetProperty("status", out var statusProp) ? statusProp.GetString() ?? "done" : "done",
                    Pages = new List<ReceiptPage>(),
                    LineItems = new List<LineItem>()
                };

                // Parse error if present
                if (root.TryGetProperty("error", out var errorProp))
                {
                    result.ErrorMessage = errorProp.GetString();
                }

                // Parse pages
                if (root.TryGetProperty("pages", out var pagesProp) && pagesProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var page in pagesProp.EnumerateArray())
                    {
                        var receiptPage = new ReceiptPage
                        {
                            PageNumber = page.TryGetProperty("page_number", out var pnProp) ? pnProp.GetInt32() : 1,
                            RawOcrText = page.TryGetProperty("raw_ocr_text", out var rtProp) ? rtProp.GetString() ?? "" : "",
                            Words = new List<OcrWord>()
                        };

                        if (page.TryGetProperty("words", out var wordsProp) && wordsProp.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var word in wordsProp.EnumerateArray())
                            {
                                var wordBox = word.TryGetProperty("box", out var boxProp) && boxProp.ValueKind == JsonValueKind.Object
                                    ? ParseBoundingBox(boxProp) ?? new BoundingBox()
                                    : new BoundingBox();

                                var ocrWord = new OcrWord
                                {
                                    Text = word.TryGetProperty("text", out var textProp) ? textProp.GetString() ?? "" : "",
                                    Box = wordBox,
                                    Confidence = word.TryGetProperty("confidence", out var confProp) ? confProp.GetDouble() : 0
                                };

                                receiptPage.Words.Add(ocrWord);
                            }
                        }

                        result.Pages.Add(receiptPage);
                    }
                }

                // Parse extracted fields
                result.VendorName = ParseExtractedField(root, "vendor_name");
                result.MerchantAddress = ParseExtractedField(root, "merchant_address");
                result.Date = ParseExtractedField(root, "date");
                result.TotalAmount = ParseExtractedField(root, "total_amount");
                result.Subtotal = ParseExtractedField(root, "subtotal");
                result.TaxAmount = ParseExtractedField(root, "tax_amount");
                result.Currency = ParseExtractedField(root, "currency");

                // Parse line items
                if (root.TryGetProperty("line_items", out var lineItemsProp) && lineItemsProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in lineItemsProp.EnumerateArray())
                    {
                        var lineItem = new LineItem
                        {
                            Description = item.TryGetProperty("description", out var descProp) ? descProp.GetString() ?? "" : "",
                            Quantity = item.TryGetProperty("quantity", out var qtyProp) ? (decimal?)qtyProp.GetDouble() : null,
                            UnitPrice = item.TryGetProperty("unit_price", out var upProp) ? (decimal?)upProp.GetDouble() : null,
                            LineTotal = item.TryGetProperty("line_total", out var ltProp) ? (decimal?)ltProp.GetDouble() : null,
                            Confidence = item.TryGetProperty("confidence", out var cProp) ? cProp.GetDouble() : 0
                        };

                        if (item.TryGetProperty("box", out var itemBoxProp) && itemBoxProp.ValueKind == JsonValueKind.Object)
                        {
                            lineItem.Box = ParseBoundingBox(itemBoxProp);
                        }

                        result.LineItems.Add(lineItem);
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse OCR result JSON");
                return new ReceiptView
                {
                    JobId = jobId,
                    Status = "failed",
                    ErrorMessage = $"Failed to parse OCR result: {ex.Message}"
                };
            }
        }

        private ExtractedField? ParseExtractedField(JsonElement root, string fieldName)
        {
            if (!root.TryGetProperty(fieldName, out var fieldProp) || fieldProp.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            return new ExtractedField
            {
                Value = fieldProp.TryGetProperty("value", out var valProp) ? valProp.GetString() ?? "" : "",
                Confidence = fieldProp.TryGetProperty("confidence", out var confProp) ? confProp.GetDouble() : 0,
                Box = fieldProp.TryGetProperty("box", out var boxProp) && boxProp.ValueKind == JsonValueKind.Object
                    ? ParseBoundingBox(boxProp)
                    : null
            };
        }

        private BoundingBox? ParseBoundingBox(JsonElement boxElement)
        {
            if (boxElement.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            return new BoundingBox
            {
                X0 = boxElement.TryGetProperty("x0", out var x0Prop) ? x0Prop.GetInt32() : 0,
                Y0 = boxElement.TryGetProperty("y0", out var y0Prop) ? y0Prop.GetInt32() : 0,
                X1 = boxElement.TryGetProperty("x1", out var x1Prop) ? x1Prop.GetInt32() : 0,
                Y1 = boxElement.TryGetProperty("y1", out var y1Prop) ? y1Prop.GetInt32() : 0
            };
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
