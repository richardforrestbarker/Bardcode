using Bardcoded.ApiService.Data;
using Bardcoded.ApiService.Data.Store;
using Bardcoded.Data.Messages;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.FeatureManagement;
using OpenTelemetry.Metrics;

namespace Bardcoded.ApiService.Providers
{
    internal class IOMapper
    {
        internal BarcodeData Map(BardcodeInjestRequest request)
        {
            return new BarcodeData()
            {
                Bard = request.Bard,
                Base64Image = request.Base64Image,
                Description = request.Description,
                ImageType = request.ImageType,
                Source = request.Source,
                Name = request.Name
            };
        }

        internal BarcodeData Map(BardcodeUpdateRequest request)
        {
            return new BarcodeData()
            {
                Id = request.Id,
                Bard = request.Bard,
                Base64Image = request.Base64Image,
                Description = request.Description,
                ImageType = request.ImageType,
                Source = request.Source,
                Name = request.Name
            };
        }

        internal BarcodeView Map(BarcodeData data)
        {
            return BarcodeView.Create(data.Bard, data.Name, data.Description, data.Base64Image, data.ImageType);
        }
    }
    public class BarcodeFetcher
    {
        private readonly Dictionary<ApiProviderConfiguration, ApiProvider> configs;
        private readonly MemoryCache cache;
        private readonly IFeatureManager features;
        private readonly IBarcodeDataContext database;
        private readonly IOMapper mapper;
        private readonly ILogger<BarcodeFetcher> logger;
        private bool useCache;
        private bool useDb;
        private bool useApis;

        public BarcodeFetcher(Dictionary<ApiProviderConfiguration, ApiProvider> configs, MemoryCache cache, IFeatureManager features, IBarcodeDataContext database, ILoggerFactory factory)
        {
            this.configs = configs;
            this.cache = cache;
            this.features = features;
            this.database = database;
            this.mapper = new IOMapper();
            this.logger = factory.CreateLogger<BarcodeFetcher>();
        }

        public async Task<BardcodeInjestRequest?> FindItem(string barcode, string barcodeType)
        {
            BardcodeInjestRequest result;
            useCache = await GetUseCache();
            useDb = await GetUseDb();
            useApis = await GetUseApis();

            if (useCache && Cached(barcode))
            {
                logger.LogInformation("Fetching from Cache.");
                result = await Cache(barcode);
                return result;
            }
            else if (!useCache)
            {
                logger.LogInformation("Fetching from Cache is turned off.");
            }

            if (useDb && await Databased(barcode))
            {
                result = await Database(barcode);
                CacheIt(barcode, result);
                return result;
            }
            else if (!useDb)
            {
                logger.LogInformation("Fetching from db is turned off.");
            }
            if (useApis)
            {
                var ingestRequest = await NetworkProviders(barcode, barcodeType);
                if (ingestRequest != null) StoreAndCacheIt(barcode, ingestRequest);
                return ingestRequest;
            }
            logger.LogInformation("Fetching from Apis is turned off.");
            return null;
        }

        private Task<bool> GetUseApis()
        {
            return features.IsEnabledAsync("FetchFromApis");
        }

        private Task<bool> GetUseDb()
        {
            return features.IsEnabledAsync("UseDatabase");
        }

        private Task<bool> GetUseCache()
        {
            return features.IsEnabledAsync("UseCache");
        }

        private async void CacheIt(string barcode, BardcodeInjestRequest result)
        {
            if (!useCache) return;
            var entry = cache.CreateEntry(barcode);
            entry.Value = result;
            entry.SetAbsoluteExpiration(DateTime.UtcNow.AddMinutes(60))
                .SetSlidingExpiration(TimeSpan.FromMinutes(5));
        }

        private void StoreAndCacheIt(string barcode, BardcodeInjestRequest result)
        {
            if (useCache) CacheIt(barcode, result);
            if (useDb) StoreIt(barcode, result);
        }

        private void StoreIt(string barcode, BardcodeInjestRequest request)
        {
            var entity = mapper.Map(request);
            database.InsertBarcode(entity);
            
            // Store provider data if available
            if (!string.IsNullOrEmpty(request.ProviderType) && !string.IsNullOrEmpty(request.ProviderJson))
            {
                database.InsertBarcodeDataProvided(new BarcodeDataProvided
                {
                    Bard = barcode,
                    LastUpdated = DateTime.UtcNow,
                    ProviderType = request.ProviderType,
                    ProviderJson = request.ProviderJson
                });
            }
        }

        private async Task<BardcodeInjestRequest> Database(string barcode)
        {
            var entity = await database.GetBarcode(barcode);
            var request = new BardcodeInjestRequest
            {
                Bard = entity.Bard,
                Name = entity.Name,
                Description = entity.Description,
                Base64Image = entity.Base64Image,
                ImageType = entity.ImageType,
                Source = entity.Source,
                WeightVolume = ""
            };
            
            // Get provider information if available
            var providerData = await database.GetBarcodeDataProvided(barcode);
            if (providerData != null)
            {
                request.ProviderType = providerData.ProviderType;
                request.ProviderJson = providerData.ProviderJson;
            }
            
            return request;
        }

        private Task<BardcodeInjestRequest> Cache(string barcode)
        {
            return Task.FromResult(cache.Get<BardcodeInjestRequest>(barcode));
        }

        private async Task<BardcodeInjestRequest?> NetworkProviders(string barcode, string barcodeType)
        {
            logger.LogInformation($"Fetching {barcode} from network providers.");
            foreach (var integration in configs)
            {
                var provider = integration.Value;
                var config = integration.Key;
                if (!config.Enabled)
                {
                    logger.LogInformation($"Skipping provider {config.Type} because it is disabled.");
                    continue;
                }
                if (!config.IsBarcodeTypeAllowed(barcodeType))
                {
                    logger.LogInformation($"Skipping provider {config.Type} due to barcode type restrictions.");
                    continue;
                }
                if (await config.IsOverRates())
                {
                    logger.LogWarning($"Skipping {config.Type} for {barcode} due to rate limiting.");
                    continue;
                }
                try
                {
                    logger.LogTrace($"Using {config.Type}");
                    var client = await provider.GetHttpClient();
                    config.Rate.Count += 1;
                    var response = await client.GetAsync(provider.GetPathForBarcode(barcode));
                    if (await provider.IsResponseKosher(response))
                    {
                        logger.LogInformation($"Successfully fetched {barcode} from {config.Type}.");
                        // Capture the full JSON response first
                        var jsonResponse = await response.Content.ReadAsStringAsync();
                        var result = await provider.Translate(response);
                        if (result != null)
                        {
                            // Create the ingest request with all the data
                            return new BardcodeInjestRequest
                            {
                                Bard = result.Code,
                                Name = result.Name,
                                Description = result.Description,
                                Base64Image = result.ImageAsBase64 ?? "",
                                ImageType = result.ImageType ?? "",
                                Source = "API",
                                WeightVolume = "",
                                ProviderType = config.Type,
                                ProviderJson = jsonResponse
                            };
                        }
                    }
                }
                catch (Exception e)
                {
                    logger.LogError($"Caught and ignoring {e.GetType()} trying to get {barcode} from {config.Type}: {e.Message}");
                }
            }
            return null;
        }

        private async Task<bool> Databased(string barcode)
        {
            return (await database.GetBarcode(barcode)) != null;
        }

        private bool Cached(string barcode)
        {
            return false;
            //return cache.TryGetValue(barcode, out _);
        }
    }
}
