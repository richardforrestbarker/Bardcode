

using Bardcoded.Data.Api;
using Bardcoded.Data.Messages;
using Microsoft.Identity.Client;
using System.Collections.Generic;
using System.Data;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;



namespace Bardcoded.ApiService.Providers;

[JsonDerivedType(typeof(UpcDatabaseApiProvider), nameof(UpcDatabaseApiProvider))]
[JsonDerivedType(typeof(ApiProviderConfiguration))]
public class ApiProviderConfiguration
{
    [JsonPropertyName("path")] public string Path { get; set; }
    [JsonPropertyName("$type")] public string Type { get; set; }
    [JsonPropertyName("key")] public string Key { get; set; }
    [JsonPropertyName("url")] public string Url { get; set; }
    [JsonPropertyName("allowedBarcodeTypes")] public ISet<string> AllowedBarcodeTypes { get; }
    [JsonExtensionData] public Dictionary<string, JsonElement> UnknownFields { get; set; }

    public virtual Task<HttpClient> GetHttpClient()
    {
        throw new NotImplementedException();
    }
    public virtual Task<bool> IsResponseKosher(HttpResponseMessage response)
    {
        return Task.FromResult(response.IsSuccessStatusCode);
    }

    public virtual Task<bool> IsOverRates()
    {
        throw new NotImplementedException();
    }

    public virtual Task<BarcodeView> Translate(HttpResponseMessage res)
    {
        throw new NotImplementedException();
    }

    public virtual Task UpdateRates()
    {
        throw new NotImplementedException();
    }

    public bool IsBarcodeTypeAllowed(string barcodeType)
    {
        return AllowedBarcodeTypes?.Contains(barcodeType) ?? false;
    }
}
public class UpcDatabaseApiProvider : ApiProviderConfiguration
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
            Console.WriteLine($"{nameof(UpcDatabaseApiProvider)}: Request to {Path} failed because {error.Error}");
        }
        else
        {
            Console.WriteLine($"UPCDatabase returned an unexpected response {parsed}");
        }
        return null;

    }

    public override Task UpdateRates()
    {
        return Task.CompletedTask;
    }

    public override Task<bool> IsOverRates()

    {
        return Task.FromResult(false);
    }

    public override async Task<bool> IsResponseKosher(HttpResponseMessage response)
    {
        if (!await base.IsResponseKosher(response)) return false;
        //var body = await response.Content.ReadFromJsonAsync<UpcDatabaseResponse>();
        //return body is UpcItemDataResponse;
        return true;
    }

    public override Task<HttpClient> GetHttpClient()
    {
        var client = new HttpClient();
        client.BaseAddress = new Uri(Url);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Key);
        return Task.FromResult(client);
    }
}


