using Bardcoded.Data.Api;
using Bardcoded.Data.Messages;
using Microsoft.Identity.Client;
using System.Collections.Generic;
using System.Data;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;



namespace Bardcoded.ApiService.Providers;

public class RateLimit
{
    [JsonPropertyName(nameof(TimeSpan))] public TimeSpan TimeSpan { get; set; }
    [JsonPropertyName(nameof(Limit))] public int Limit { get; set; }
}

public class Rate
{
    [JsonPropertyName(nameof(Count))] public int Count { get; set; }
    [JsonPropertyName(nameof(NextReset))] public DateTime NextReset { get; set; }
}

public abstract class ApiProvider(ApiProviderConfiguration c)
{
    public ApiProviderConfiguration Config { get; } = c;
    public abstract Task<HttpClient> GetHttpClient();
    public virtual Task<bool> IsResponseKosher(HttpResponseMessage response)
    {
        return Task.FromResult(response?.IsSuccessStatusCode ?? false);
    }
    public abstract Task<BarcodeView> Translate(HttpResponseMessage res);
    public virtual string GetPathForBarcode(string barcode)
    {
        return Config.Path.Replace("{barcode}", barcode);
    }
}
public class ApiProviderConfiguration
{
    [JsonPropertyName(nameof(ApiProviderConfiguration.Path))] public string Path { get; set; }
    [JsonPropertyName("Type")] public string Type { get; set; }
    [JsonPropertyName(nameof(Key))] public string Key { get; set; }
    [JsonPropertyName(nameof(Enabled))] public bool Enabled { get; set; }
    [JsonPropertyName(nameof(Url))] public string Url { get; set; }
    [JsonPropertyName(nameof(AllowedBarcodeTypes))] public ISet<string> AllowedBarcodeTypes { get; init; }
    [JsonPropertyName(nameof(RateLimit))] public RateLimit RateLimit { get; set; }
    [JsonPropertyName(nameof(Rate))] public Rate Rate { get; set; } = new Rate();
    [JsonExtensionData] public Dictionary<string, JsonElement> UnknownFields { get; set; }
    public virtual Task<bool> IsOverRates()
    {
        return Task.FromResult(Rate != null && RateLimit != null && Rate.Count > RateLimit.Limit);
    }

    public virtual Task UpdateRates()
    {
        throw new NotImplementedException();
    }

    public bool IsBarcodeTypeAllowed(string barcodeType)
    {
        return AllowedBarcodeTypes?.Contains(barcodeType.ToUpperInvariant()) ?? false;
    }

}
public class UpcDatabaseApiProvider(ApiProviderConfiguration c) : ApiProvider(c)
{
    public override async Task<BarcodeView> Translate(HttpResponseMessage res)
    {
        var body = res.Content;
        var parsed = await body.ReadFromJsonAsync<UpcDatabaseResponse>();
        if (parsed is UpcItemDataResponse item)
        {
            var images = item.Images?.Select(UrlEncoder.Default.Encode).Aggregate(string.Empty, (c, n) => c + "," + n);
            if (images == string.Empty) images = null;
            return BarcodeView.Create(item.Barcode, item.Title, item.Description, images, "png");
        }
        else if (parsed is FailedUpcResponse error)
        {
            Console.WriteLine($"{nameof(UpcDatabaseApiProvider)}: Request to {Config.Path} failed because {error.Error}");
        }
        else
        {
            Console.WriteLine($"UPCDatabase returned an unexpected response {parsed}");
        }
        return null;

    }
    public override async Task<bool> IsResponseKosher(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<UpcDatabaseResponse>();
        return body is UpcItemDataResponse;
    }

    public override Task<HttpClient> GetHttpClient()
    {
        var client = new HttpClient();
        client.BaseAddress = new Uri(Config.Url);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Config.Key);
        return Task.FromResult(client);
    }
}

public class OpenFoodFactsApiProvider(ApiProviderConfiguration c) : ApiProvider(c)
{
    public override async Task<BarcodeView> Translate(HttpResponseMessage res)
    {
        var body = res.Content;
        var parsed = await body.ReadFromJsonAsync<OpenFoodFactsResponse>();

        if (parsed is OpenFoodFactsProductResponse productResponse && productResponse.Product != null)
        {
            var product = productResponse.Product;
            var name = product.ProductName ?? product.GenericName ?? "Unknown Product";
            var description = BuildDescription(product);
            var imageUrl = product.ImageFrontUrl ?? product.ImageUrl;

            return BarcodeView.Create(
                productResponse.Code ?? string.Empty,
                name,
                description,
                imageUrl != null ? UrlEncoder.Default.Encode(imageUrl) : null,
                "jpg"
            );
        }
        else
        {
            Console.WriteLine($"{nameof(OpenFoodFactsApiProvider)}: Product not found or invalid response");
        }

        return null;
    }

    private string BuildDescription(OpenFoodFactsProduct product)
    {
        var parts = new List<string>();

        if (!string.IsNullOrEmpty(product.Brands))
            parts.Add($"Brand: {product.Brands}");

        if (!string.IsNullOrEmpty(product.Quantity))
            parts.Add($"Quantity: {product.Quantity}");

        if (!string.IsNullOrEmpty(product.Categories))
            parts.Add($"Categories: {product.Categories}");

        return parts.Count > 0 ? string.Join(". ", parts) : "No description available";
    }



    public override Task<HttpClient> GetHttpClient()
    {
        var client = new HttpClient();
        client.BaseAddress = new Uri(Config.Url);
        var ua = Config.UnknownFields?["UserAgent"] ?? null;
        if (ua.HasValue && ua.Value.ValueKind == JsonValueKind.String)
        {
            client.DefaultRequestHeaders.Add("User-Agent", ua.Value.GetString());
        }
        else
        {
            client.DefaultRequestHeaders.Add("User-Agent", "Bardcode/dev (richardforrestbarker+openfoodfacts@gmail.com)");
        }
        if (Config.Url.EndsWith(".net")) // indicates using upstream's stage environment
        {
            client.DefaultRequestHeaders.Add("Authorization", "Basic " + js_btoa("off:off"));
        }
        return Task.FromResult(client);
    }

    public string js_btoa(string toEncode)
    {
        byte[] bytes = Encoding.GetEncoding(28591).GetBytes(toEncode);
        string toReturn = System.Convert.ToBase64String(bytes);
        return toReturn;
    }
}

public class BarcodeLookupApiProvider(ApiProviderConfiguration c) : ApiProvider(c)
{
    private const int MaxFeaturesToInclude = 3;

    public override async Task<BarcodeView> Translate(HttpResponseMessage res)
    {
        var body = res.Content;
        var contentString = await body.ReadAsStringAsync();

        // Try to deserialize as product response first
        BarcodeLookupProductResponse? productResponse = null;
        BarcodeLookupErrorResponse? errorResponse = null;

        try
        {
            productResponse = JsonSerializer.Deserialize<BarcodeLookupProductResponse>(contentString);
        }
        catch (JsonException jsonEx)
        {
            Console.WriteLine($"{nameof(BarcodeLookupApiProvider)}: Failed to deserialize product response: {jsonEx.Message}");
            // Try error response
            try
            {
                errorResponse = JsonSerializer.Deserialize<BarcodeLookupErrorResponse>(contentString);
            }
            catch (JsonException errorEx)
            {
                Console.WriteLine($"{nameof(BarcodeLookupApiProvider)}: Failed to deserialize error response: {errorEx.Message}");
            }
        }

        if (productResponse?.Products != null && productResponse.Products.Length > 0)
        {
            var product = productResponse.Products[0]; // Take the first product

            // Prioritize title, then label, then product_name
            var name = !string.IsNullOrEmpty(product.Title) ? product.Title :
                       !string.IsNullOrEmpty(product.Label) ? product.Label :
                       !string.IsNullOrEmpty(product.ProductName) ? product.ProductName :
                       "Unknown Product";

            var description = BuildDescription(product);
            var imageUrl = product.Images?.FirstOrDefault();

            return BarcodeView.Create(
                product.BarcodeNumber ?? string.Empty,
                name,
                description,
                imageUrl != null ? UrlEncoder.Default.Encode(imageUrl) : null,
                "jpg"
            );
        }
        else if (errorResponse != null)
        {
            Console.WriteLine($"{nameof(BarcodeLookupApiProvider)}: Request failed - {errorResponse.Error}: {errorResponse.Message}");
        }
        else
        {
            Console.WriteLine($"{nameof(BarcodeLookupApiProvider)}: Product not found or invalid response");
        }

        return null;
    }

    private string BuildDescription(BarcodeLookupProduct product)
    {
        var parts = new List<string>();

        if (!string.IsNullOrEmpty(product.Description))
            parts.Add(product.Description);

        if (!string.IsNullOrEmpty(product.Brand))
            parts.Add($"Brand: {product.Brand}");

        if (!string.IsNullOrEmpty(product.Manufacturer))
            parts.Add($"Manufacturer: {product.Manufacturer}");

        if (!string.IsNullOrEmpty(product.Category))
            parts.Add($"Category: {product.Category}");

        if (!string.IsNullOrEmpty(product.Size))
            parts.Add($"Size: {product.Size}");

        if (product.Features != null && product.Features.Length > 0)
            parts.Add($"Features: {string.Join(", ", product.Features.Take(MaxFeaturesToInclude))}");

        return parts.Count > 0 ? string.Join(". ", parts) : "No description available";
    }

    public override Task<HttpClient> GetHttpClient()
    {
        var client = new HttpClient();
        client.BaseAddress = new Uri(Config.Url);
        return Task.FromResult(client);
    }

    public override string GetPathForBarcode(string barcode)
    {
        var path = base.GetPathForBarcode(barcode);
        // Append the API key if it's set
        if (!string.IsNullOrEmpty(Config.Key))
        {
            path += Config.Key;
        }
        return path;
    }
}


