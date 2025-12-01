using Bardcoded.ApiService.Controllers;
using Bardcoded.ApiService.Ocr;
using Bardcoded.ApiService.Ocr.Messages;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Bardcoded.Tests
{
    public class DocumentControllerTests
    {
        private readonly DocumentController _controller;
        private readonly FakeDocumentProcessor _fakeProcessor;

        public DocumentControllerTests()
        {
            var logger = new FakeLogger<DocumentController>();
            _fakeProcessor = new FakeDocumentProcessor();
            _controller = new DocumentController(logger, _fakeProcessor);
        }

        [Fact]
        public async Task Preprocess_WithNoImage_ReturnsBadRequest()
        {
            // Arrange
            var request = new PreprocessingRequest
            {
                ImageBase64 = ""
            };

            // Act
            var result = await _controller.Preprocess(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequestResult.Value);
        }

        [Fact]
        public async Task Preprocess_WithValidImage_ReturnsOk()
        {
            // Arrange
            var request = new PreprocessingRequest
            {
                ImageBase64 = "base64imagedata",
                Deskew = true,
                Denoise = false,
                FuzzPercent = 30,
                ContrastType = "sigmoidal"
            };

            _fakeProcessor.SetPreprocessingResult(new PreprocessingResult
            {
                JobId = "test-job",
                Status = "done",
                ImageBase64 = "processed-base64",
                Width = 400,
                Height = 600
            });

            // Act
            var result = await _controller.Preprocess(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<PreprocessingResult>(okResult.Value);
            Assert.Equal("done", response.Status);
            Assert.NotNull(response.ImageBase64);
        }

        [Fact]
        public async Task Preprocess_WhenProcessorFails_ReturnsServerError()
        {
            // Arrange
            var request = new PreprocessingRequest
            {
                ImageBase64 = "base64imagedata"
            };

            _fakeProcessor.SetPreprocessingResult(new PreprocessingResult
            {
                JobId = "test-job",
                Status = "failed",
                Error = "Preprocessing failed"
            });

            // Act
            var result = await _controller.Preprocess(request);

            // Assert
            var statusResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, statusResult.StatusCode);
        }

        [Fact]
        public async Task RunOcr_WithNoImage_ReturnsBadRequest()
        {
            // Arrange
            var request = new OcrRequest
            {
                ImageBase64 = ""
            };

            // Act
            var result = await _controller.RunOcr(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequestResult.Value);
        }

        [Fact]
        public async Task RunOcr_WithValidImage_ReturnsOk()
        {
            // Arrange
            var request = new OcrRequest
            {
                ImageBase64 = "preprocessed-base64",
                OcrEngine = "paddle"
            };

            _fakeProcessor.SetOcrResult(new OcrResult
            {
                JobId = "test-job",
                Status = "done",
                RawOcrText = "GROCERY STORE Total $7.01",
                Words = new List<Data.Messages.OcrWord>
                {
                    new() { Text = "GROCERY", Box = new Data.Messages.BoundingBox { X0 = 100, Y0 = 50, X1 = 200, Y1 = 80 }, Confidence = 0.98 }
                }
            });

            // Act
            var result = await _controller.RunOcr(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<OcrResult>(okResult.Value);
            Assert.Equal("done", response.Status);
            Assert.NotNull(response.Words);
            Assert.Single(response.Words);
        }

        [Fact]
        public async Task RunInference_WithNoOcrResult_ReturnsBadRequest()
        {
            // Arrange
            var request = new InferenceRequest
            {
                OcrResult = null!,
                ImageBase64 = "base64image"
            };

            // Act
            var result = await _controller.RunInference(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequestResult.Value);
        }

        [Fact]
        public async Task RunInference_WithNoImage_ReturnsBadRequest()
        {
            // Arrange
            var request = new InferenceRequest
            {
                OcrResult = new OcrResult { JobId = "test", Status = "done" },
                ImageBase64 = ""
            };

            // Act
            var result = await _controller.RunInference(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequestResult.Value);
        }

        [Fact]
        public async Task RunInference_WithValidRequest_ReturnsOk()
        {
            // Arrange
            var request = new InferenceRequest
            {
                OcrResult = new OcrResult 
                { 
                    JobId = "test-ocr", 
                    Status = "done",
                    Words = new List<Data.Messages.OcrWord>()
                },
                ImageBase64 = "base64image",
                ModelType = "donut"
            };

            _fakeProcessor.SetInferenceResult(new InferenceResult
            {
                JobId = "test-inference",
                Status = "done",
                VendorName = new Data.Messages.ExtractedField { Value = "GROCERY STORE", Confidence = 0.95 },
                TotalAmount = new Data.Messages.ExtractedField { Value = "7.01", Confidence = 0.90 }
            });

            // Act
            var result = await _controller.RunInference(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<InferenceResult>(okResult.Value);
            Assert.Equal("done", response.Status);
            Assert.NotNull(response.VendorName);
            Assert.NotNull(response.TotalAmount);
        }

        [Fact]
        public async Task GetStatus_WithValidJobId_ReturnsStatus()
        {
            // Arrange
            var jobId = "test-job-123";
            _fakeProcessor.SetJobStatus(jobId, new JobStatus
            {
                JobId = jobId,
                Status = "processing",
                Phase = "preprocessing",
                Progress = 50
            });

            // Act
            var result = await _controller.GetStatus(jobId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var status = Assert.IsType<JobStatus>(okResult.Value);
            Assert.Equal(jobId, status.JobId);
            Assert.Equal(50, status.Progress);
        }

        [Fact]
        public async Task GetStatus_WithInvalidJobId_ReturnsNotFound()
        {
            // Act
            var result = await _controller.GetStatus("non-existent-job");

            // Assert
            Assert.IsType<NotFoundObjectResult>(result);
        }
    }

    /// <summary>
    /// Fake document processor for testing.
    /// </summary>
    public class FakeDocumentProcessor : IDocumentProcessor
    {
        private PreprocessingResult? _preprocessingResult;
        private OcrResult? _ocrResult;
        private InferenceResult? _inferenceResult;
        private readonly Dictionary<string, JobStatus> _jobStatuses = new();

        public void SetPreprocessingResult(PreprocessingResult result) => _preprocessingResult = result;
        public void SetOcrResult(OcrResult result) => _ocrResult = result;
        public void SetInferenceResult(InferenceResult result) => _inferenceResult = result;
        public void SetJobStatus(string jobId, JobStatus status) => _jobStatuses[jobId] = status;

        public Task<PreprocessingResult> PreprocessImageAsync(PreprocessingRequest request)
        {
            return Task.FromResult(_preprocessingResult ?? new PreprocessingResult
            {
                JobId = Guid.NewGuid().ToString(),
                Status = "done",
                ImageBase64 = "test-base64"
            });
        }

        public Task<OcrResult> RunOcrAsync(OcrRequest request)
        {
            return Task.FromResult(_ocrResult ?? new OcrResult
            {
                JobId = Guid.NewGuid().ToString(),
                Status = "done"
            });
        }

        public Task<InferenceResult> RunInferenceAsync(InferenceRequest request)
        {
            return Task.FromResult(_inferenceResult ?? new InferenceResult
            {
                JobId = Guid.NewGuid().ToString(),
                Status = "done"
            });
        }

        public Task<JobStatus?> GetJobStatusAsync(string jobId)
        {
            _jobStatuses.TryGetValue(jobId, out var status);
            return Task.FromResult(status);
        }
    }
}
