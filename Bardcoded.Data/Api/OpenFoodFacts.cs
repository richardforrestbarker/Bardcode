using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bardcoded.Data.Api;

[JsonConverter(typeof(OpenFoodFactsResponseConverter))]
public class OpenFoodFactsResponse
{
    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("status_verbose")]
    public string? StatusVerbose { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? UnknownFields { get; set; }
}

public class OpenFoodFactsProductResponse : OpenFoodFactsResponse
{
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("product")]
    public OpenFoodFactsProduct? Product { get; set; }
}

public class OpenFoodFactsProduct
{
    [JsonPropertyName("product_name")]
    public string? ProductName { get; set; }

    [JsonPropertyName("generic_name")]
    public string? GenericName { get; set; }

    [JsonPropertyName("brands")]
    public string? Brands { get; set; }

    [JsonPropertyName("categories")]
    public string? Categories { get; set; }

    [JsonPropertyName("image_url")]
    public string? ImageUrl { get; set; }

    [JsonPropertyName("image_front_url")]
    public string? ImageFrontUrl { get; set; }

    [JsonPropertyName("image_front_small_url")]
    public string? ImageFrontSmallUrl { get; set; }

    [JsonPropertyName("quantity")]
    public string? Quantity { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? UnknownFields { get; set; }
}

internal class OpenFoodFactsResponseConverter : JsonConverter<OpenFoodFactsResponse>
{
    public override bool CanConvert(Type typeToConvert) => 
        typeof(OpenFoodFactsResponse).IsAssignableFrom(typeToConvert);

    public override OpenFoodFactsResponse Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException($"Expected StartObject token, got {reader.TokenType}");
        }

        using (var jsonDocument = JsonDocument.ParseValue(ref reader))
        {
            var rawJson = jsonDocument.RootElement.GetRawText();

            if (!jsonDocument.RootElement.TryGetProperty("status", out var statusProperty))
            {
                throw new JsonException("Status property not found in Open Food Facts response");
            }

            var status = statusProperty.GetInt32();

            // Status 1 means product found, 0 means not found
            if (status == 1)
            {
                return JsonSerializer.Deserialize<OpenFoodFactsProductResponse>(rawJson, options) 
                    ?? new OpenFoodFactsResponse { Status = status };
            }
            else
            {
                return JsonSerializer.Deserialize<OpenFoodFactsResponse>(rawJson, options) 
                    ?? new OpenFoodFactsResponse { Status = status };
            }
        }
    }

    public override void Write(Utf8JsonWriter writer, OpenFoodFactsResponse response, JsonSerializerOptions options)
    {
        if (response is OpenFoodFactsProductResponse productResponse)
        {
            JsonSerializer.Serialize(writer, productResponse, options);
        }
        else
        {
            JsonSerializer.Serialize(writer, response, options);
        }
    }
}
