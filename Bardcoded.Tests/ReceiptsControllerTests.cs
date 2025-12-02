using Bardcoded.ApiService.Controllers;
using Bardcoded.Data;
using Bardcoded.Data.Messages;
using DocumentProcessor.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Bardcoded.Tests
{
    public class ReceiptsControllerTests
    {
        private readonly ReceiptsController _controller;
        private readonly FakeReceiptProcessor _fakeProcessor;
        private readonly OcrConfiguration _config;

        public ReceiptsControllerTests()
        {
            var logger = new FakeLogger<ReceiptsController>();
            _config = new OcrConfiguration
            {
                MaxFileSize = 10 * 1024 * 1024,
                TempStoragePath = "./temp/test"
            };
            _fakeProcessor = new FakeReceiptProcessor();
            _controller = new ReceiptsController(logger, _fakeProcessor, _config);
            
            // Mock URL helper to avoid null reference
            var urlHelper = new FakeUrlHelper();
            _controller.Url = urlHelper;
        }

        [Fact]
        public async Task UploadReceipt_WithNoFiles_ReturnsBadRequest()
        {
            // Arrange
            var files = new FormFileCollection();

            // Act
            var result = await _controller.UploadReceipt(files);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequestResult.Value);
        }

        [Fact]
        public async Task UploadReceipt_WithValidFile_ReturnsOkWithJobId()
        {
            // Arrange
            var files = new FormFileCollection();
            var file = CreateFakeImageFile("receipt.jpg", 1024);
            files.Add(file);

            // Act
            var result = await _controller.UploadReceipt(files);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
            
            var response = okResult.Value as ReceiptUploadResponse;
            Assert.NotNull(response);
            Assert.NotNull(response?.JobId);
            Assert.Equal("processing", response?.Status);
        }

        [Fact]
        public async Task UploadReceipt_WithOversizedFile_ReturnsPayloadTooLarge()
        {
            // Arrange
            var files = new FormFileCollection();
            var file = CreateFakeImageFile("large.jpg", 20 * 1024 * 1024); // 20MB
            files.Add(file);

            // Act
            var result = await _controller.UploadReceipt(files);

            // Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(413, statusCodeResult.StatusCode);
        }

        [Fact]
        public async Task UploadReceipt_WithNonImageFile_ReturnsBadRequest()
        {
            // Arrange
            var files = new FormFileCollection();
            var file = CreateFakeFile("document.txt", "text/plain", 1024);
            files.Add(file);

            // Act
            var result = await _controller.UploadReceipt(files);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequestResult.Value);
        }

        [Fact]
        public async Task GetStatus_WithValidJobId_ReturnsStatus()
        {
            // Arrange
            var jobId = "test-job-123";
            _fakeProcessor.AddStatus(jobId, new ReceiptStatusResponse
            {
                JobId = jobId,
                Status = "processing",
                Progress = 50
            });

            // Act
            var result = await _controller.GetStatus(jobId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var status = okResult.Value as ReceiptStatusResponse;
            Assert.NotNull(status);
            Assert.Equal(jobId, status?.JobId);
            Assert.Equal(50, status?.Progress);
        }

        [Fact]
        public async Task GetStatus_WithInvalidJobId_ReturnsNotFound()
        {
            // Act
            var result = await _controller.GetStatus("non-existent-job");

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            Assert.NotNull(notFoundResult.Value);
        }

        [Fact]
        public async Task GetResult_WithCompletedJob_ReturnsResult()
        {
            // Arrange
            var jobId = "completed-job";
            _fakeProcessor.AddResult(jobId, new ReceiptView
            {
                JobId = jobId,
                Status = "done",
                VendorName = new ExtractedField { Value = "Test Store", Confidence = 0.95 }
            });

            // Act
            var result = await _controller.GetResult(jobId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var receiptView = okResult.Value as ReceiptView;
            Assert.NotNull(receiptView);
            Assert.Equal("done", receiptView?.Status);
        }

        [Fact]
        public async Task GetResult_WithProcessingJob_ReturnsTooEarly()
        {
            // Arrange
            var jobId = "processing-job";
            _fakeProcessor.AddResult(jobId, new ReceiptView
            {
                JobId = jobId,
                Status = "processing"
            });

            // Act
            var result = await _controller.GetResult(jobId);

            // Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(425, statusCodeResult.StatusCode);
        }

        private IFormFile CreateFakeImageFile(string fileName, long size)
        {
            return CreateFakeFile(fileName, "image/jpeg", size);
        }

        private IFormFile CreateFakeFile(string fileName, string contentType, long size)
        {
            var content = new byte[size];
            var stream = new MemoryStream(content);
            return new FormFile(stream, 0, size, "files", fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = contentType
            };
        }
    }

    // Fake implementations for testing
    public class FakeReceiptProcessor : IReceiptProcessor
    {
        private readonly Dictionary<string, ReceiptStatusResponse> _statuses = new();
        private readonly Dictionary<string, ReceiptView> _results = new();

        public void AddStatus(string jobId, ReceiptStatusResponse status)
        {
            _statuses[jobId] = status;
        }

        public void AddResult(string jobId, ReceiptView result)
        {
            _results[jobId] = result;
        }

        public Task<string> ProcessReceiptAsync(IFormFileCollection files, ReceiptUploadRequest request)
        {
            var jobId = Guid.NewGuid().ToString();
            _statuses[jobId] = new ReceiptStatusResponse
            {
                JobId = jobId,
                Status = "processing",
                Progress = 0
            };
            return Task.FromResult(jobId);
        }

        public Task<ReceiptStatusResponse?> GetStatusAsync(string jobId)
        {
            _statuses.TryGetValue(jobId, out var status);
            return Task.FromResult(status);
        }

        public Task<ReceiptView?> GetResultAsync(string jobId)
        {
            _results.TryGetValue(jobId, out var result);
            return Task.FromResult(result);
        }
    }

    public class FakeLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }

    public class FakeUrlHelper : IUrlHelper
    {
        public ActionContext ActionContext => throw new NotImplementedException();

        public string? Action(UrlActionContext actionContext) => $"/api/receipts/{actionContext.Action?.ToLower()}";
        public string? Content(string? contentPath) => contentPath;
        public bool IsLocalUrl(string? url) => true;
        public string? Link(string? routeName, object? values) => null;
        public string? RouteUrl(UrlRouteContext routeContext) => null;
    }
}

