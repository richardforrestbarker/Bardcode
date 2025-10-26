using Bunit;
using Bardcoded.Wasm.Pages.Ocr;
using Microsoft.Extensions.DependencyInjection;

namespace Bardcoded.Tests
{
    public class ProductOcrTests : Bunit.TestContext
    {
        public ProductOcrTests()
        {
            // BUnit will provide stub implementations for services by default
        }

        [Theory]
        [InlineData("123456789012")]
        [InlineData("0987654321098")]
        public void ProductOcr_RendersCorrectly(string barcode)
        {
            // Arrange & Act
            var component = RenderComponent<Product>(parameters => parameters.Add(p => p.Barcode, barcode));

            // Assert
            Assert.NotNull(component);
            var markup = component.Markup;
            Assert.Contains("Product OCR", markup);
            Assert.Contains("Upload Product Images", markup);
            Assert.Contains(barcode, markup);
        }

        [Theory]
        [InlineData("123456789012")]
        [InlineData("0987654321098")]
        public void ProductOcr_DisplaysBarcodeParameter(string barcode)
        {
            // Arrange & Act
            var component = RenderComponent<Product>(parameters => parameters.Add(p => p.Barcode, barcode));

            // Assert
            var barcodeInput = component.Find("input[readonly]");
            Assert.NotNull(barcodeInput);
            Assert.Equal(barcode, barcodeInput.GetAttribute("value"));
        }

        [Theory]
        [InlineData("#imageUpload", "image/*")]
        public void ProductOcr_HasImageUploadInput(string elementId, string acceptAttribute)
        {
            // Arrange & Act
            var component = RenderComponent<Product>(parameters => parameters.Add(p => p.Barcode, "123456789012"));

            // Assert
            var imageUpload = component.Find(elementId);
            Assert.NotNull(imageUpload);
            Assert.Equal(acceptAttribute, imageUpload.GetAttribute("accept"));
        }

        [Theory]
        [InlineData("Product OCR")]
        [InlineData("Upload Product Images")]
        public void ProductOcr_ContainsExpectedText(string expectedText)
        {
            // Arrange & Act
            var component = RenderComponent<Product>(parameters => parameters.Add(p => p.Barcode, "123456789012"));

            // Assert
            var markup = component.Markup;
            Assert.Contains(expectedText, markup);
        }

        [Theory]
        [InlineData("Extracted Text")]
        [InlineData("Uploaded Images")]
        public void ProductOcr_InitializesWithEmptyState(string notExpectedText)
        {
            // Arrange & Act
            var component = RenderComponent<Product>(parameters => parameters.Add(p => p.Barcode, "123456789012"));

            // Assert
            var markup = component.Markup;
            Assert.DoesNotContain(notExpectedText, markup);
        }
    }
}
