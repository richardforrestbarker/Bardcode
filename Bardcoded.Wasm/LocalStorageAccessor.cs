using Bardcoded.Data.Exceptions;
using Bardcoded.Data.Messages;
using Bardcoded.Shaded.Microsoft.AspNetCore.Mvc;
using Microsoft.JSInterop;
using System.Text.Json;

namespace Bardcoded.Wasm
{
    public class CachedBarcodeLocalStorage : LocalStorageAccessor
    {
        private const string CachedBardsKey = "cachedBards";
        public CachedBarcodeLocalStorage(IJSRuntime jsRuntime, IFeatureManager features) : base(jsRuntime)
        {
            Features = features;
        }

        public IFeatureManager Features { get; }

        public async Task PutItemIntoCache(BarcodeView data)
        {
            if (!await Features.IsEnabledAsync("UseLocalStorage"))
            {
                Console.WriteLine("Not using local storage.");
                return;
            }
            var cachedBards = await GetValueAsync<Dictionary<string, BarcodeView>>(CachedBardsKey) ?? new Dictionary<string, BarcodeView>();
            cachedBards[data.Code] = data;
            await SetValueAsync(CachedBardsKey, cachedBards);
        }

        public async Task<BarcodeView?> TryGetItemFromLocalStorage(string bard)
        {
            if (!await Features.IsEnabledAsync("UseLocalStorage"))
            {
                Console.WriteLine("Not using local storage.");
                return null;
            }
            var items = await GetValueAsync<Dictionary<string, BarcodeView>>(CachedBardsKey);
            if (items == null)
            {
                Console.WriteLine("No Bards have ever been cached.");
                return null;
            }
            if (items.TryGetValue(bard, out BarcodeView? item))
            {
                Console.WriteLine("Found a cached bard.");
                return item;
            }
            else return null;
        }

    }
    public class CreateBarcodeLocalStorage : LocalStorageAccessor
    {
        private const string CreateRequestsLocalStorageKey = "createRequests";
        public CreateBarcodeLocalStorage(IJSRuntime jsRuntime, IFeatureManager features) : base(jsRuntime)
        {
            Features = features;
        }

        public IFeatureManager Features { get; }

        public async Task TryAddToLocalStorage(BardcodeInjestRequest data)
        {
            if (!await Features.IsEnabledAsync("UseLocalStorage"))
            {
                Console.WriteLine("Not using local storage.");
            }
            Dictionary<string, BardcodeInjestRequest> createRequests = await GetValueAsync<Dictionary<string, BardcodeInjestRequest>>(CreateRequestsLocalStorageKey) ?? new Dictionary<string, BardcodeInjestRequest>();
            if (createRequests.TryGetValue(data.Bard, out BardcodeInjestRequest? exists))
            {
                throw new DataConflictException("That barcode is already cached and ready to be stored when the app is back online.", exists);
            }
            createRequests[data.Bard] = data;
            await SetValueAsync("createRequests", createRequests);
        }
    }

    public class LocalStorageAccessor : IAsyncDisposable
    {
        private Lazy<IJSObjectReference> _accessorJsRef = new();
        private readonly IJSRuntime _jsRuntime;

        public LocalStorageAccessor(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        private async Task WaitForReference()
        {
            if (_accessorJsRef.IsValueCreated is false)
            {
                _accessorJsRef = new(await _jsRuntime.InvokeAsync<IJSObjectReference>("import", "/js/localstorage.js"));
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_accessorJsRef.IsValueCreated)
            {
                await _accessorJsRef.Value.DisposeAsync();
            }
        }
        public async Task<T> GetValueAsync<T>(string key)
        {
            await WaitForReference();
            var json = await _accessorJsRef.Value.InvokeAsync<string>("get", key);
            if (json == null) return default;
            var result = (T)JsonSerializer.Deserialize(json, typeof(T));
            return result;
        }

        public async Task SetValueAsync<T>(string key, T value)
        {
            await WaitForReference();
            var val = JsonSerializer.Serialize(value);
            await _accessorJsRef.Value.InvokeVoidAsync("set", key, val);
        }

        public async Task Clear()
        {
            await WaitForReference();
            await _accessorJsRef.Value.InvokeVoidAsync("clear");
        }

        public async Task RemoveAsync(string key)
        {
            await WaitForReference();
            await _accessorJsRef.Value.InvokeVoidAsync("remove", key);
        }
    }
}
