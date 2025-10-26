using Bardcoded.Data;
using Bardcoded.Data.Exceptions;
using Bardcoded.Data.Messages;
using Bardcoded.Shaded.Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Bardcoded.Wasm.Clients
{
    public class BardcodeApiHttpClient : HttpClient
    {
        public BardcodeApiHttpClient(BardcodedApiConfiguration config,
           CachedBarcodeLocalStorage known, CreateBarcodeLocalStorage create
            )
        {
            Config = config;
            Known = known;
            Create = create;
            BaseAddress = new Uri(config.BaseAddress!);
        }

        public BardcodedApiConfiguration Config { get; }
        public CachedBarcodeLocalStorage Known { get; }
        public CreateBarcodeLocalStorage Create { get; }

        public async Task<BardcodeInjestRequest?> CreateItem(BardcodeInjestRequest data)
        {
            HttpResponseMessage res;
            try
            {
                res = await PostAsync("/item", JsonContent.Create(data, mediaType: MediaTypeHeaderValue.Parse("application/json")));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Encountered an error. {ex}");
                // the server is unreachable for some reason... is it down? is something in the way? does the device have internet?
                await Create.TryAddToLocalStorage(data);
                throw new OfflineException("Cannot create that until the app is back online. The create request has been cached on the device and will be added as soon as the app is back online.");
            }
            try
            {
                // the request actually made it out and back in at this point.
                res.EnsureSuccessStatusCode();
                return JsonSerializer.Deserialize<BardcodeInjestRequest>(res.Content.ReadAsStream());
            }
            catch (HttpRequestException ex)
            {
                if (res.StatusCode.Equals(HttpStatusCode.Conflict))
                {
                    throw new DataConflictException("That barcode already exists in the database.", data);
                }
                var problemJson = await res.Content.ReadAsStringAsync();
                throw new ApiErrorResponseException($"The API indicated a problem: {ex.Message}", data.Bard, res.StatusCode, JsonSerializer.Deserialize<ProblemDetails?>(problemJson), ex);
            }
        }

        public async Task<BarcodeView?> GetItem(String bard, string barcodeType)
        {
            HttpResponseMessage response;
            try
            {
                response = await GetAsync($"item?bard={UrlEncoder.Default.Encode(bard)}&barcodeType={UrlEncoder.Default.Encode(barcodeType)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Encountered an error. {ex}");
                // Try to get from local storage if available
                // For now, return null since we're changing the API
                Console.WriteLine($"Encountered an error calling the api.");
                return null;
            }
            if (!response.IsSuccessStatusCode)
            {
                var res = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Received a problem from the API. {res}");
                return null;
            }
            if (response.StatusCode == HttpStatusCode.NonAuthoritativeInformation)
            {
                // we found the barcode in a network provider but not in our database
                // this makes bardcode essentially a mirror until the data is stored.
                var injest = await response.Content.ReadFromJsonAsync<BardcodeInjestRequest?>();
                if (injest != null) throw new CreateBarcodeRequired(injest!);
            }
            return await response.Content.ReadFromJsonAsync<BarcodeView?>();
        }

        public async Task<List<BarcodeView>> GetItems()
        {
            try
            {
                var response = await GetAsync($"item/all");
                if (!response.IsSuccessStatusCode)
                {
                    var res = response.Content.ReadFromJsonAsync<ProblemDetails>();
                    Console.WriteLine(res);
                }
                ;
                var x = await response.Content.ReadFromJsonAsync<List<BarcodeView>>();
                if (x == null) return null;
                return x;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return null;
            }
        }

        public async Task<Health> Healthcheck()
        {
            try
            {
                var res = await GetAsync("health");
                var health = await res.Content.ReadFromJsonAsync<Health>();
                Console.WriteLine($"received heath-check response: {health} ");
                if (!res.IsSuccessStatusCode) return Health.Down;
                else return health;

            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return Health.Down;
            }
        }

        public async ValueTask DisposeAsync()
        {
            Create?.DisposeAsync();
            Known?.DisposeAsync();
        }
    }
}

