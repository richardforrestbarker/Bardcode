using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Bardcoded.Data.Messages
{
    public class BarcodeView
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string? ImageAsBase64 { get; set; }
        public string? ImageType { get; set; }
        public string? ProviderType { get; set; }
        public string? ProviderJson { get; set; }

        public static BarcodeView Create(string Code, string name, string description, string? ImageAsBase64, string? imageType)
        {
            return new BarcodeView() { Name = name, Description = description, ImageAsBase64 = ImageAsBase64, Code = Code, ImageType = imageType };
        }

        public static BarcodeView Create(string Code, string name, string description, string? ImageAsBase64, string? imageType, string? providerType)
        {
            return new BarcodeView() { Name = name, Description = description, ImageAsBase64 = ImageAsBase64, Code = Code, ImageType = imageType, ProviderType = providerType };
        }
    }
    public class BardcodeInjestRequest
    {
        [Description("The barcode.")]
        [Required(ErrorMessage = $"{nameof(Bard)} is required.")]
        [StringLength(100, ErrorMessage = $"{nameof(Bard)} can't be longer than 100 characters.")]
        [JsonPropertyName("bard")]
        public string Bard { get; set; }

        [Description("Where the barcode was loaded into the system from - it's injest method.")]
        [Required(ErrorMessage = $"{nameof(Source)} is required.")]
        [StringLength(100, ErrorMessage = $"{nameof(Source)} can't be longer than 100 characters.")]
        [JsonPropertyName("Source")]
        public string Source { get; set; }
        [Description("The name of the item")]
        [Required(ErrorMessage = $"{nameof(Name)} is required.")]
        [StringLength(100, ErrorMessage = $"{nameof(Name)} can't be longer than 100 characters.")]
        [JsonPropertyName("Name")]
        public string Name { get; set; }

        [Description("A description of the item.")]
        [StringLength(4096, ErrorMessage = $"{nameof(Description)} can't be longer than 4096 characters.")]
        [JsonPropertyName("description")]
        public string Description { get; set; }

        [Description("An image of the item's label / brand.")]
        [Required(ErrorMessage = $"Image is required.")]
        [JsonPropertyName("base64Image")]
        public string Base64Image { get; set; }
        [Description("The image's type; i.e jpg, png.")]
        [Required(ErrorMessage = $"Image type is required.")]
        [JsonPropertyName("imageType")]
        public string ImageType { get; set; }

        [Description("The \"size\" of the item.")]
        [Required(ErrorMessage = $"{nameof(WeightVolume)} is required.")]
        [StringLength(1024, ErrorMessage = $"{nameof(WeightVolume)} can't be longer than 1024 characters.")]
        [JsonPropertyName("weightVolume")]
        public string WeightVolume { get; set; }

        [Description("The provider that returned this data.")]
        [JsonPropertyName("providerType")]
        public string? ProviderType { get; set; }

        [Description("The full JSON response from the provider.")]
        [JsonPropertyName("providerJson")]
        public string? ProviderJson { get; set; }

    }


    public class BardcodeUpdateRequest
    {
        public BardcodeUpdateRequest(string Bard, string Source, Guid id)
        {
            this.Bard = Bard; // these are un-update-able
            this.Source = Source;
            Id = id;
        }
        public string Bard { get; }
        public string Source { get; }
        public Guid Id { get; }


        [Description("The name of the item")]
        [Required(ErrorMessage = $"{nameof(Name)} is required.")]
        [StringLength(100, ErrorMessage = $"{nameof(Name)} can't be longer than 100 characters.")]
        public string Name { get; set; }

        [Description("A description of the item.")]
        [StringLength(4096, ErrorMessage = $"{nameof(Description)} can't be longer than 4096 characters.")]
        public string Description { get; set; }

        [Description("An image of the item's label / brand.")]
        [Required(ErrorMessage = $"Image is required.")]
        public string Base64Image { get; set; }

        [Description("The image's type; i.e jpg, png.")]
        [Required(ErrorMessage = $"Image type is required.")]
        public string ImageType { get; set; }

        [Description("The \"size\" of the item.")]
        [Required(ErrorMessage = $"{nameof(WeightVolume)} is required.")]
        [StringLength(1024, ErrorMessage = $"{nameof(WeightVolume)} can't be longer than 1024 characters.")]
        public string WeightVolume { get; set; }
    }
}
