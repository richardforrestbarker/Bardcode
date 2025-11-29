using Bardcoded.Data.Messages;
using System.Text.Json;
using Xunit;

namespace Bardcoded.Tests
{
    public class ReceiptMetadataTests
    {
        [Fact]
        public void BoundingBox_CanBeCreatedAndSerialized()
        {
            // Arrange
            var box = new BoundingBox
            {
                X0 = 100,
                Y0 = 200,
                X1 = 300,
                Y1 = 400
            };

            // Act
            var json = JsonSerializer.Serialize(box);
            var deserialized = JsonSerializer.Deserialize<BoundingBox>(json);

            // Assert
            Assert.NotNull(deserialized);
            Assert.Equal(100, deserialized?.X0);
            Assert.Equal(200, deserialized?.Y0);
            Assert.Equal(300, deserialized?.X1);
            Assert.Equal(400, deserialized?.Y1);
        }

        [Fact]
        public void ExtractedField_SerializesCorrectly()
        {
            // Arrange
            var field = new ExtractedField
            {
                Value = "Sample Store",
                Confidence = 0.95,
                Box = new BoundingBox { X0 = 50, Y0 = 50, X1 = 200, Y1 = 100 }
            };

            // Act
            var json = JsonSerializer.Serialize(field);
            var deserialized = JsonSerializer.Deserialize<ExtractedField>(json);

            // Assert
            Assert.NotNull(deserialized);
            Assert.Equal("Sample Store", deserialized?.Value);
            Assert.Equal(0.95, deserialized?.Confidence);
            Assert.NotNull(deserialized?.Box);
        }

        [Fact]
        public void LineItem_CanBeCreatedWithAllFields()
        {
            // Arrange & Act
            var lineItem = new LineItem
            {
                Description = "Product ABC",
                Quantity = 2.0m,
                UnitPrice = 10.50m,
                LineTotal = 21.00m,
                Box = new BoundingBox { X0 = 50, Y0 = 300, X1 = 550, Y1 = 340 },
                Confidence = 0.89
            };

            // Assert
            Assert.Equal("Product ABC", lineItem.Description);
            Assert.Equal(2.0m, lineItem.Quantity);
            Assert.Equal(10.50m, lineItem.UnitPrice);
            Assert.Equal(21.00m, lineItem.LineTotal);
            Assert.Equal(0.89, lineItem.Confidence);
            Assert.NotNull(lineItem.Box);
        }

        [Fact]
        public void ReceiptView_CanContainMultiplePages()
        {
            // Arrange
            var receipt = new ReceiptView
            {
                JobId = "test-123",
                Status = "done"
            };

            receipt.Pages.Add(new ReceiptPage
            {
                PageNumber = 1,
                RawOcrText = "Page 1 text"
            });

            receipt.Pages.Add(new ReceiptPage
            {
                PageNumber = 2,
                RawOcrText = "Page 2 text"
            });

            // Act & Assert
            Assert.Equal(2, receipt.Pages.Count);
            Assert.Equal("Page 1 text", receipt.Pages[0].RawOcrText);
            Assert.Equal("Page 2 text", receipt.Pages[1].RawOcrText);
        }

        [Fact]
        public void ReceiptView_SerializesCompleteReceipt()
        {
            // Arrange
            var receipt = new ReceiptView
            {
                JobId = "test-job-456",
                Status = "done",
                VendorName = new ExtractedField
                {
                    Value = "Test Grocery",
                    Confidence = 0.96,
                    Box = new BoundingBox { X0 = 100, Y0 = 50, X1 = 300, Y1 = 100 }
                },
                TotalAmount = new ExtractedField
                {
                    Value = "45.99",
                    Confidence = 0.98,
                    Box = new BoundingBox { X0 = 400, Y0 = 500, X1 = 500, Y1 = 550 }
                }
            };

            receipt.LineItems.Add(new LineItem
            {
                Description = "Milk",
                Quantity = 1,
                UnitPrice = 3.99m,
                LineTotal = 3.99m,
                Confidence = 0.92
            });

            // Act
            var json = JsonSerializer.Serialize(receipt, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            var deserialized = JsonSerializer.Deserialize<ReceiptView>(json);

            // Assert
            Assert.NotNull(deserialized);
            Assert.Equal("test-job-456", deserialized?.JobId);
            Assert.Equal("done", deserialized?.Status);
            Assert.Equal("Test Grocery", deserialized?.VendorName?.Value);
            Assert.Equal("45.99", deserialized?.TotalAmount?.Value);
            Assert.Single(deserialized?.LineItems ?? new List<LineItem>());
        }

        [Fact]
        public void ReceiptStatusResponse_TracksProgress()
        {
            // Arrange & Act
            var status = new ReceiptStatusResponse
            {
                JobId = "job-789",
                Status = "processing",
                Progress = 75,
                Message = "Extracting fields..."
            };

            // Assert
            Assert.Equal("job-789", status.JobId);
            Assert.Equal("processing", status.Status);
            Assert.Equal(75, status.Progress);
            Assert.Equal("Extracting fields...", status.Message);
        }

        public static TheoryData<string, int> StatusProgressData => new()
        {
            { "processing", 25 },
            { "processing", 50 },
            { "processing", 75 },
            { "done", 100 },
            { "failed", 0 }
        };

        [Theory]
        [MemberData(nameof(StatusProgressData))]
        public void ReceiptStatusResponse_SupportsVariousStates(string status, int progress)
        {
            // Arrange & Act
            var statusResponse = new ReceiptStatusResponse
            {
                JobId = "test",
                Status = status,
                Progress = progress
            };

            // Assert
            Assert.Equal(status, statusResponse.Status);
            Assert.Equal(progress, statusResponse.Progress);
        }

        [Fact]
        public void OcrWord_ContainsTextAndBox()
        {
            // Arrange & Act
            var word = new OcrWord
            {
                Text = "TOTAL",
                Box = new BoundingBox { X0 = 100, Y0 = 200, X1 = 200, Y1 = 250 },
                Confidence = 0.98
            };

            // Assert
            Assert.Equal("TOTAL", word.Text);
            Assert.Equal(0.98, word.Confidence);
            Assert.NotNull(word.Box);
            Assert.Equal(100, word.Box.X0);
        }
    }
}
