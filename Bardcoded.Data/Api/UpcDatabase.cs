using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks.Dataflow;


namespace Bardcoded.Data.Api;

[JsonConverter(typeof(UpcDatabaseResponseConverter))]
public class UpcDatabaseResponse
{
    //[JsonPropertyName("success")]
    //public string Success { get; set; } = "false";

    [JsonExtensionData]
    public Dictionary<string, JsonElement> UnknownFields { get; set; }
}

public class FailedUpcResponse : UpcDatabaseResponse
{
    [JsonPropertyName("error")]
    public string Error { get; set; } = string.Empty;
}



public class UpcItemDataResponse : UpcDatabaseResponse
{
    [JsonPropertyName("barcode")]
    public string Barcode { get; set; } = "0123456789510";
    [JsonPropertyName("title")]
    public string Title { get; set; } = "General Item Name";
    [JsonPropertyName("alias")]
    public string Alias { get; set; } = "ShortName";
    [JsonPropertyName("description")]
    public string Description { get; set; } = "A sentence or two describing the item. It should cover the basics.";
    [JsonPropertyName("brand")]
    public string Brand { get; set; } = "GoodBrand";
    [JsonPropertyName("manufacturer")]
    public string Manufacturer { get; set; } = "Manufacturer";
    [JsonPropertyName("mpn")]
    public string Mpn { get; set; } = "Manu Part Number";
    [JsonPropertyName("msrb")]
    public decimal Msrp { get; set; } = 9.99M;
    [JsonPropertyName("ASIN")]
    public string ASIN { get; set; } = "ASIN";
    [JsonPropertyName("category")]
    public string Category { get; set; } = "Plumbing";
    [JsonPropertyName("image")]
    public string[] Images { get; set; } = null;
}

//public class UpcDatabaseProductResponseTypeResolver : DefaultJsonTypeInfoResolver
//{
//    public override JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options)
//    {
//        JsonTypeInfo jsonTypeInfo = base.GetTypeInfo(type, options);

//        Type basePointType = typeof(UpcDatabaseResponse);
//        if (jsonTypeInfo.Type == basePointType)
//        {
//            jsonTypeInfo.PolymorphismOptions = new JsonPolymorphismOptions
//            {
//                TypeDiscriminatorPropertyName = "success",
//                IgnoreUnrecognizedTypeDiscriminators = false,
//                UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToBaseType,
//                DerivedTypes = {
//                    new JsonDerivedType(typeof(FailedUpcResponse)),
//                    new JsonDerivedType(typeof(UpcItemDataResponse))
//                },
//            };
//            jsonTypeInfo.OnDeserializing = thing => Console.WriteLine(nameof(UpcDatabaseProductResponseTypeResolver) + " is deserializing a " + thing.GetType() + " kind of thing.\n{}", thing);
//        }

//        return jsonTypeInfo;
//    }
//}

internal class UpcDatabaseResponseConverter : JsonConverter<UpcDatabaseResponse>
{
    public override bool CanConvert(Type typeToConvert) => typeof(UpcDatabaseResponse).IsAssignableFrom(typeToConvert);
    public override UpcDatabaseResponse Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException($"didn't expect that {reader.TokenType} @ {reader.TokenStartIndex} - parsing a Upc database response fails.");
        }
        using (var jsonDocument = JsonDocument.ParseValue(ref reader))
        {
            var possibleUpcData = jsonDocument.RootElement.GetRawText();

            if (!jsonDocument.RootElement.TryGetProperty("success", out var typeProperty))
            {
                throw new JsonException("Discriminator not found.");
            }
            //if (typeProperty.GetString() == null  || typeProperty.GetString().Equals(string.Empty))
            //{

            //    throw new JsonException("Discriminator is null / not parsable.");
            //}

            else if (typeProperty.GetBoolean())
            {
                return JsonSerializer.Deserialize<UpcItemDataResponse>(possibleUpcData);
            }
            else
            {
                return JsonSerializer.Deserialize<FailedUpcResponse>(possibleUpcData);
            }
        }
    }
    public override void Write(Utf8JsonWriter writer, UpcDatabaseResponse response, JsonSerializerOptions options)
    {
        if (response is FailedUpcResponse bad)
        {
            JsonSerializer.Serialize(writer, bad);
        }
        else if (response is UpcItemDataResponse good)
        {
            JsonSerializer.Serialize(writer, good);
        }
    }
}
