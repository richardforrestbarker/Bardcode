using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Bardcoded.Data;
using Bardcoded.Data.Messages;

namespace Bardcoded.ApiService.Ocr
{
    /// <summary>
    /// Interface for document processing service for the OCR pipeline live view.
    /// </summary>
    public interface IDocumentProcessor
    {
        /// <summary>
        /// Run preprocessing on an image and return the result as base64.
        /// </summary>
        Task<PreprocessingResult> PreprocessImageAsync(PreprocessingRequest request);
        
        /// <summary>
        /// Run OCR on a preprocessed image.
        /// </summary>
        Task<OcrResult> RunOcrAsync(OcrRequest request);
        
        /// <summary>
        /// Run model inference on OCR results.
        /// </summary>
        Task<InferenceResult> RunInferenceAsync(InferenceRequest request);
        
        /// <summary>
        /// Get the status of a job.
        /// </summary>
        Task<JobStatus?> GetJobStatusAsync(string jobId);
    }

    /// <summary>
    /// Request for preprocessing an image.
    /// </summary>
    public class PreprocessingRequest
    {
        /// <summary>
        /// Base64 encoded source image.
        /// </summary>
        public required string ImageBase64 { get; set; }
        
        /// <summary>
        /// Original filename for format detection.
        /// </summary>
        public string? Filename { get; set; }
        
        /// <summary>
        /// Job identifier for tracking.
        /// </summary>
        public string? JobId { get; set; }
        
        /// <summary>
        /// Apply denoising.
        /// </summary>
        public bool Denoise { get; set; } = false;
        
        /// <summary>
        /// Apply deskewing.
        /// </summary>
        public bool Deskew { get; set; } = true;
        
        /// <summary>
        /// Fuzz percentage for background removal (0-100).
        /// </summary>
        public int FuzzPercent { get; set; } = 30;
        
        /// <summary>
        /// Deskew threshold percentage (0-100).
        /// </summary>
        public int DeskewThreshold { get; set; } = 40;
        
        /// <summary>
        /// Contrast enhancement type: 'sigmoidal', 'linear', or 'none'.
        /// </summary>
        public string ContrastType { get; set; } = "sigmoidal";
        
        /// <summary>
        /// Contrast strength for sigmoidal type (1-10).
        /// </summary>
        public double ContrastStrength { get; set; } = 3.0;
        
        /// <summary>
        /// Contrast midpoint percentage (0-200).
        /// </summary>
        public int ContrastMidpoint { get; set; } = 120;
    }

    /// <summary>
    /// Result from preprocessing.
    /// </summary>
    public class PreprocessingResult
    {
        public required string JobId { get; set; }
        public required string Status { get; set; }
        public string? ImageBase64 { get; set; }
        public string? ImageFormat { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string? Error { get; set; }
    }

    /// <summary>
    /// Request for running OCR.
    /// </summary>
    public class OcrRequest
    {
        /// <summary>
        /// Path to preprocessed image file or base64 encoded image.
        /// </summary>
        public required string ImageBase64 { get; set; }
        
        /// <summary>
        /// Job identifier for tracking.
        /// </summary>
        public string? JobId { get; set; }
        
        /// <summary>
        /// OCR engine to use: 'paddle' or 'tesseract'.
        /// </summary>
        public string OcrEngine { get; set; } = "paddle";
        
        /// <summary>
        /// Target DPI for resampling.
        /// </summary>
        public int TargetDpi { get; set; } = 300;
        
        /// <summary>
        /// Device for OCR: 'auto', 'cuda', or 'cpu'.
        /// </summary>
        public string Device { get; set; } = "auto";
    }

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

    /// <summary>
    /// Request for running model inference.
    /// </summary>
    public class InferenceRequest
    {
        /// <summary>
        /// Path to OCR result JSON or the OcrResult object.
        /// </summary>
        public required OcrResult OcrResult { get; set; }
        
        /// <summary>
        /// Base64 encoded image for visual features.
        /// </summary>
        public required string ImageBase64 { get; set; }
        
        /// <summary>
        /// Job identifier for tracking.
        /// </summary>
        public string? JobId { get; set; }
        
        /// <summary>
        /// Model name or path.
        /// </summary>
        public string Model { get; set; } = "naver-clova-ix/donut-base-finetuned-cord-v2";
        
        /// <summary>
        /// Model type: 'donut', 'idefics2', or 'layoutlmv3'.
        /// </summary>
        public string ModelType { get; set; } = "donut";
        
        /// <summary>
        /// Device for inference: 'auto', 'cuda', or 'cpu'.
        /// </summary>
        public string Device { get; set; } = "auto";
    }

    /// <summary>
    /// Result from model inference.
    /// </summary>
    public class InferenceResult
    {
        public required string JobId { get; set; }
        public required string Status { get; set; }
        public ExtractedField? VendorName { get; set; }
        public ExtractedField? MerchantAddress { get; set; }
        public ExtractedField? Date { get; set; }
        public ExtractedField? TotalAmount { get; set; }
        public ExtractedField? Subtotal { get; set; }
        public ExtractedField? TaxAmount { get; set; }
        public ExtractedField? Currency { get; set; }
        public List<LineItem> LineItems { get; set; } = new();
        public string? Error { get; set; }
    }

    /// <summary>
    /// Job status for tracking long-running operations.
    /// </summary>
    public class JobStatus
    {
        public required string JobId { get; set; }
        public required string Status { get; set; }
        public string? Phase { get; set; }
        public int Progress { get; set; }
        public string? Message { get; set; }
        public string? Error { get; set; }
    }

    /// <summary>
    /// Document processor implementation that calls Python CLI.
    /// </summary>
    public class DocumentProcessor : IDocumentProcessor
    {
        private readonly ILogger<DocumentProcessor> _logger;
        private readonly OcrConfiguration _config;
        private readonly ConcurrentDictionary<string, JobStatus> _jobs = new();

        public DocumentProcessor(ILogger<DocumentProcessor> logger, OcrConfiguration config)
        {
            _logger = logger;
            _config = config;
        }

        public async Task<PreprocessingResult> PreprocessImageAsync(PreprocessingRequest request)
        {
            var jobId = request.JobId ?? Guid.NewGuid().ToString();
            
            _jobs[jobId] = new JobStatus
            {
                JobId = jobId,
                Status = "processing",
                Phase = "preprocessing",
                Progress = 0,
                Message = "Starting preprocessing"
            };

            var result = new PreprocessingResult
            {
                JobId = jobId,
                Status = "processing"
            };

            try
            {
                // Save base64 image to temp file
                var tempDir = Path.Combine(_config.TempStoragePath, jobId);
                Directory.CreateDirectory(tempDir);
                
                var extension = GetImageExtension(request.Filename);
                var inputPath = Path.Combine(tempDir, $"input{extension}");
                var imageBytes = Convert.FromBase64String(request.ImageBase64);
                await File.WriteAllBytesAsync(inputPath, imageBytes);

                // Build CLI arguments
                var pythonPath = FindPythonExecutable();
                var cliPath = GetCliPath();
                
                var args = new List<string>
                {
                    cliPath,
                    "preprocess",
                    "--image", inputPath,
                    "--output-format", "base64",
                    "--job-id", jobId
                };

                if (request.Denoise)
                    args.Add("--denoise");
                
                if (request.Deskew)
                    args.Add("--deskew");
                
                args.AddRange(new[]
                {
                    "--fuzz-percent", request.FuzzPercent.ToString(),
                    "--deskew-threshold", request.DeskewThreshold.ToString(),
                    "--contrast-type", request.ContrastType,
                    "--contrast-strength", request.ContrastStrength.ToString(),
                    "--contrast-midpoint", request.ContrastMidpoint.ToString()
                });

                _jobs[jobId] = new JobStatus
                {
                    JobId = jobId,
                    Status = "processing",
                    Phase = "preprocessing",
                    Progress = 50,
                    Message = "Running preprocessing pipeline"
                };

                // Run CLI
                var output = await RunPythonCliAsync(pythonPath, args);
                
                // Parse result
                var cliResult = JsonSerializer.Deserialize<JsonElement>(output);
                
                if (cliResult.TryGetProperty("status", out var statusProp) && 
                    statusProp.GetString() == "done")
                {
                    result.Status = "done";
                    result.ImageBase64 = cliResult.TryGetProperty("image_base64", out var imgProp) 
                        ? imgProp.GetString() 
                        : null;
                    result.ImageFormat = cliResult.TryGetProperty("image_format", out var fmtProp) 
                        ? fmtProp.GetString() 
                        : "png";
                    result.Width = cliResult.TryGetProperty("width", out var wProp) 
                        ? wProp.GetInt32() 
                        : 0;
                    result.Height = cliResult.TryGetProperty("height", out var hProp) 
                        ? hProp.GetInt32() 
                        : 0;
                }
                else
                {
                    result.Status = "failed";
                    result.Error = cliResult.TryGetProperty("error", out var errProp) 
                        ? errProp.GetString() 
                        : "Unknown error";
                }

                _jobs[jobId] = new JobStatus
                {
                    JobId = jobId,
                    Status = result.Status,
                    Phase = "preprocessing",
                    Progress = 100,
                    Message = result.Status == "done" ? "Preprocessing complete" : result.Error
                };

                // Clean up temp files
                try
                {
                    Directory.Delete(tempDir, true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to clean up temp directory for job {JobId}", jobId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during preprocessing for job {JobId}", jobId);
                result.Status = "failed";
                result.Error = ex.Message;
                
                _jobs[jobId] = new JobStatus
                {
                    JobId = jobId,
                    Status = "failed",
                    Phase = "preprocessing",
                    Progress = 0,
                    Error = ex.Message
                };
            }

            return result;
        }

        public async Task<OcrResult> RunOcrAsync(OcrRequest request)
        {
            var jobId = request.JobId ?? Guid.NewGuid().ToString();
            
            _jobs[jobId] = new JobStatus
            {
                JobId = jobId,
                Status = "processing",
                Phase = "ocr",
                Progress = 0,
                Message = "Starting OCR"
            };

            var result = new OcrResult
            {
                JobId = jobId,
                Status = "processing"
            };

            try
            {
                // Save base64 image to temp file
                var tempDir = Path.Combine(_config.TempStoragePath, jobId);
                Directory.CreateDirectory(tempDir);
                
                var inputPath = Path.Combine(tempDir, "preprocessed.png");
                var imageBytes = Convert.FromBase64String(request.ImageBase64);
                await File.WriteAllBytesAsync(inputPath, imageBytes);

                // Build CLI arguments
                var pythonPath = FindPythonExecutable();
                var cliPath = GetCliPath();
                
                var args = new List<string>
                {
                    cliPath,
                    "ocr",
                    "--image", inputPath,
                    "--job-id", jobId,
                    "--ocr-engine", request.OcrEngine,
                    "--target-dpi", request.TargetDpi.ToString(),
                    "--device", request.Device
                };

                _jobs[jobId] = new JobStatus
                {
                    JobId = jobId,
                    Status = "processing",
                    Phase = "ocr",
                    Progress = 50,
                    Message = "Running OCR engine"
                };

                // Run CLI
                var output = await RunPythonCliAsync(pythonPath, args);
                
                // Parse result
                var cliResult = JsonSerializer.Deserialize<JsonElement>(output);
                
                if (cliResult.TryGetProperty("status", out var statusProp) && 
                    statusProp.GetString() == "done")
                {
                    result.Status = "done";
                    result.RawOcrText = cliResult.TryGetProperty("raw_ocr_text", out var textProp) 
                        ? textProp.GetString() 
                        : "";
                    result.ImageWidth = cliResult.TryGetProperty("image_width", out var wProp) 
                        ? wProp.GetInt32() 
                        : 0;
                    result.ImageHeight = cliResult.TryGetProperty("image_height", out var hProp) 
                        ? hProp.GetInt32() 
                        : 0;

                    // Parse words
                    if (cliResult.TryGetProperty("words", out var wordsProp))
                    {
                        foreach (var wordElem in wordsProp.EnumerateArray())
                        {
                            var box = new BoundingBox();
                            if (wordElem.TryGetProperty("box", out var boxProp))
                            {
                                box.X0 = boxProp.TryGetProperty("x0", out var x0) ? x0.GetInt32() : 0;
                                box.Y0 = boxProp.TryGetProperty("y0", out var y0) ? y0.GetInt32() : 0;
                                box.X1 = boxProp.TryGetProperty("x1", out var x1) ? x1.GetInt32() : 0;
                                box.Y1 = boxProp.TryGetProperty("y1", out var y1) ? y1.GetInt32() : 0;
                            }

                            result.Words.Add(new OcrWord
                            {
                                Text = wordElem.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "",
                                Box = box,
                                Confidence = wordElem.TryGetProperty("confidence", out var c) ? c.GetDouble() : 0
                            });
                        }
                    }
                }
                else
                {
                    result.Status = "failed";
                    result.Error = cliResult.TryGetProperty("error", out var errProp) 
                        ? errProp.GetString() 
                        : "Unknown error";
                }

                _jobs[jobId] = new JobStatus
                {
                    JobId = jobId,
                    Status = result.Status,
                    Phase = "ocr",
                    Progress = 100,
                    Message = result.Status == "done" ? "OCR complete" : result.Error
                };

                // Clean up temp files
                try
                {
                    Directory.Delete(tempDir, true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to clean up temp directory for job {JobId}", jobId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during OCR for job {JobId}", jobId);
                result.Status = "failed";
                result.Error = ex.Message;
                
                _jobs[jobId] = new JobStatus
                {
                    JobId = jobId,
                    Status = "failed",
                    Phase = "ocr",
                    Progress = 0,
                    Error = ex.Message
                };
            }

            return result;
        }

        public async Task<InferenceResult> RunInferenceAsync(InferenceRequest request)
        {
            var jobId = request.JobId ?? Guid.NewGuid().ToString();
            
            _jobs[jobId] = new JobStatus
            {
                JobId = jobId,
                Status = "processing",
                Phase = "inference",
                Progress = 0,
                Message = "Starting model inference"
            };

            var result = new InferenceResult
            {
                JobId = jobId,
                Status = "processing"
            };

            try
            {
                // Save base64 image and OCR result to temp files
                var tempDir = Path.Combine(_config.TempStoragePath, jobId);
                Directory.CreateDirectory(tempDir);
                
                var imagePath = Path.Combine(tempDir, "image.png");
                var imageBytes = Convert.FromBase64String(request.ImageBase64);
                await File.WriteAllBytesAsync(imagePath, imageBytes);

                var ocrResultPath = Path.Combine(tempDir, "ocr_result.json");
                var ocrResultJson = JsonSerializer.Serialize(request.OcrResult);
                await File.WriteAllTextAsync(ocrResultPath, ocrResultJson);

                // Build CLI arguments
                var pythonPath = FindPythonExecutable();
                var cliPath = GetCliPath();
                
                var args = new List<string>
                {
                    cliPath,
                    "inference",
                    "--ocr-result", ocrResultPath,
                    "--image", imagePath,
                    "--job-id", jobId,
                    "--model", request.Model,
                    "--model-type", request.ModelType,
                    "--device", request.Device
                };

                _jobs[jobId] = new JobStatus
                {
                    JobId = jobId,
                    Status = "processing",
                    Phase = "inference",
                    Progress = 50,
                    Message = "Running model inference"
                };

                // Run CLI
                var output = await RunPythonCliAsync(pythonPath, args);
                
                // Parse result
                var cliResult = JsonSerializer.Deserialize<JsonElement>(output);
                
                if (cliResult.TryGetProperty("status", out var statusProp) && 
                    statusProp.GetString() == "done")
                {
                    result.Status = "done";
                    result.VendorName = ParseExtractedField(cliResult, "vendor_name");
                    result.MerchantAddress = ParseExtractedField(cliResult, "merchant_address");
                    result.Date = ParseExtractedField(cliResult, "date");
                    result.TotalAmount = ParseExtractedField(cliResult, "total_amount");
                    result.Subtotal = ParseExtractedField(cliResult, "subtotal");
                    result.TaxAmount = ParseExtractedField(cliResult, "tax_amount");
                    result.Currency = ParseExtractedField(cliResult, "currency");

                    // Parse line items
                    if (cliResult.TryGetProperty("line_items", out var itemsProp))
                    {
                        foreach (var itemElem in itemsProp.EnumerateArray())
                        {
                            result.LineItems.Add(new LineItem
                            {
                                Description = itemElem.TryGetProperty("description", out var d) ? d.GetString() : null,
                                Quantity = itemElem.TryGetProperty("quantity", out var q) ? (decimal?)q.GetDouble() : null,
                                UnitPrice = itemElem.TryGetProperty("unit_price", out var u) ? (decimal?)u.GetDouble() : null,
                                LineTotal = itemElem.TryGetProperty("line_total", out var l) ? (decimal?)l.GetDouble() : null,
                                Confidence = itemElem.TryGetProperty("confidence", out var c) ? c.GetDouble() : 0
                            });
                        }
                    }
                }
                else
                {
                    result.Status = "failed";
                    result.Error = cliResult.TryGetProperty("error", out var errProp) 
                        ? errProp.GetString() 
                        : "Unknown error";
                }

                _jobs[jobId] = new JobStatus
                {
                    JobId = jobId,
                    Status = result.Status,
                    Phase = "inference",
                    Progress = 100,
                    Message = result.Status == "done" ? "Inference complete" : result.Error
                };

                // Clean up temp files
                try
                {
                    Directory.Delete(tempDir, true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to clean up temp directory for job {JobId}", jobId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during inference for job {JobId}", jobId);
                result.Status = "failed";
                result.Error = ex.Message;
                
                _jobs[jobId] = new JobStatus
                {
                    JobId = jobId,
                    Status = "failed",
                    Phase = "inference",
                    Progress = 0,
                    Error = ex.Message
                };
            }

            return result;
        }

        public Task<JobStatus?> GetJobStatusAsync(string jobId)
        {
            _jobs.TryGetValue(jobId, out var status);
            return Task.FromResult(status);
        }

        private static ExtractedField? ParseExtractedField(JsonElement root, string fieldName)
        {
            if (!root.TryGetProperty(fieldName, out var fieldProp) || fieldProp.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            BoundingBox? box = null;
            if (fieldProp.TryGetProperty("box", out var boxProp) && boxProp.ValueKind == JsonValueKind.Object)
            {
                box = new BoundingBox
                {
                    X0 = boxProp.TryGetProperty("x0", out var x0) ? x0.GetInt32() : 0,
                    Y0 = boxProp.TryGetProperty("y0", out var y0) ? y0.GetInt32() : 0,
                    X1 = boxProp.TryGetProperty("x1", out var x1) ? x1.GetInt32() : 0,
                    Y1 = boxProp.TryGetProperty("y1", out var y1) ? y1.GetInt32() : 0
                };
            }

            return new ExtractedField
            {
                Value = fieldProp.TryGetProperty("value", out var valProp) ? valProp.GetString() ?? "" : "",
                Confidence = fieldProp.TryGetProperty("confidence", out var confProp) ? confProp.GetDouble() : 0,
                Box = box
            };
        }

        private static string GetImageExtension(string? filename)
        {
            if (string.IsNullOrEmpty(filename))
                return ".png";
            
            var ext = Path.GetExtension(filename).ToLowerInvariant();
            return string.IsNullOrEmpty(ext) ? ".png" : ext;
        }

        private async Task<string> RunPythonCliAsync(string pythonPath, List<string> args)
        {
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
                throw new TimeoutException("CLI process timed out after 5 minutes");
            }

            if (!string.IsNullOrWhiteSpace(error))
            {
                _logger.LogWarning("CLI stderr: {Error}", error);
            }

            _logger.LogInformation("CLI completed with exit code {ExitCode}", process.ExitCode);

            if (process.ExitCode != 0)
            {
                throw new Exception($"CLI failed with exit code {process.ExitCode}: {error}");
            }

            return output;
        }

        private string FindPythonExecutable()
        {
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
            
            return "python3";
        }

        private string GetCliPath()
        {
            var cliPath = _config.PythonServicePath;
            
            if (!Path.IsPathRooted(cliPath))
            {
                cliPath = Path.Combine(Directory.GetCurrentDirectory(), cliPath);
            }
            
            cliPath = Path.GetFullPath(cliPath);
            
            if (!File.Exists(cliPath))
            {
                _logger.LogWarning("CLI path not found at {CliPath}, trying alternate locations", cliPath);
                
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
    }
}
