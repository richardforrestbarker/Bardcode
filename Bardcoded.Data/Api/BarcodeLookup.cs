using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bardcoded.Data.Api;

public class BarcodeLookupResponse
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? UnknownFields { get; set; }
}

public class BarcodeLookupProductResponse : BarcodeLookupResponse
{
    [JsonPropertyName("products")]
    public BarcodeLookupProduct[]? Products { get; set; }
}

public class BarcodeLookupErrorResponse : BarcodeLookupResponse
{
    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

public class BarcodeLookupProduct
{
    [JsonPropertyName("barcode_number")]
    public string? BarcodeNumber { get; set; }

    [JsonPropertyName("barcode_type")]
    public string? BarcodeType { get; set; }

    [JsonPropertyName("barcode_formats")]
    public string? BarcodeFormats { get; set; }

    [JsonPropertyName("mpn")]
    public string? Mpn { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("asin")]
    public string? Asin { get; set; }

    [JsonPropertyName("product_name")]
    public string? ProductName { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("manufacturer")]
    public string? Manufacturer { get; set; }

    [JsonPropertyName("brand")]
    public string? Brand { get; set; }

    [JsonPropertyName("label")]
    public string? Label { get; set; }

    [JsonPropertyName("author")]
    public string? Author { get; set; }

    [JsonPropertyName("publisher")]
    public string? Publisher { get; set; }

    [JsonPropertyName("artist")]
    public string? Artist { get; set; }

    [JsonPropertyName("actor")]
    public string? Actor { get; set; }

    [JsonPropertyName("director")]
    public string? Director { get; set; }

    [JsonPropertyName("studio")]
    public string? Studio { get; set; }

    [JsonPropertyName("genre")]
    public string? Genre { get; set; }

    [JsonPropertyName("audience_rating")]
    public string? AudienceRating { get; set; }

    [JsonPropertyName("ingredients")]
    public string? Ingredients { get; set; }

    [JsonPropertyName("nutrition_facts")]
    public string? NutritionFacts { get; set; }

    [JsonPropertyName("color")]
    public string? Color { get; set; }

    [JsonPropertyName("format")]
    public string? Format { get; set; }

    [JsonPropertyName("package_quantity")]
    public string? PackageQuantity { get; set; }

    [JsonPropertyName("size")]
    public string? Size { get; set; }

    [JsonPropertyName("length")]
    public string? Length { get; set; }

    [JsonPropertyName("width")]
    public string? Width { get; set; }

    [JsonPropertyName("height")]
    public string? Height { get; set; }

    [JsonPropertyName("weight")]
    public string? Weight { get; set; }

    [JsonPropertyName("release_date")]
    public string? ReleaseDate { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("features")]
    public string[]? Features { get; set; }

    [JsonPropertyName("images")]
    public string[]? Images { get; set; }

    [JsonPropertyName("stores")]
    public BarcodeLookupStore[]? Stores { get; set; }

    [JsonPropertyName("reviews")]
    public BarcodeLookupReview[]? Reviews { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? UnknownFields { get; set; }
}

public class BarcodeLookupStore
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("currency")]
    public string? Currency { get; set; }

    [JsonPropertyName("currency_symbol")]
    public string? CurrencySymbol { get; set; }

    [JsonPropertyName("price")]
    public string? Price { get; set; }

    [JsonPropertyName("sale_price")]
    public string? SalePrice { get; set; }

    [JsonPropertyName("tax")]
    public string? Tax { get; set; }

    [JsonPropertyName("link")]
    public string? Link { get; set; }

    [JsonPropertyName("item_group_id")]
    public string? ItemGroupId { get; set; }

    [JsonPropertyName("availability")]
    public string? Availability { get; set; }

    [JsonPropertyName("condition")]
    public string? Condition { get; set; }

    [JsonPropertyName("shipping")]
    public string? Shipping { get; set; }

    [JsonPropertyName("last_update")]
    public string? LastUpdate { get; set; }
}

public class BarcodeLookupReview
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("rating")]
    public string? Rating { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("review")]
    public string? Review { get; set; }

    [JsonPropertyName("date")]
    public string? Date { get; set; }
}

