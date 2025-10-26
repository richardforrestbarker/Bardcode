using Bardcoded.ApiService.Data;
using Bardcoded.ApiService.Data.Store;
using Bardcoded.Data.Messages;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;
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
        public async Task<BarcodeView?> FindItemInCache(string barcode, string barcodeType)
        {
            if (await GetUseCache() && Cached(barcode))
            {
                logger.LogInformation("Fetching from Cache.");
                return await Cache(barcode);
            }
            else if (!useCache)
            {
                logger.LogInformation("Fetching from Cache is turned off.");
            }
            else
            {
                logger.LogInformation("Item not found in cache.");
            }
            return null;
        }
        public async Task<BarcodeView?> FindItemInDatabase(string barcode, string barcodeType)
        {
            if (await GetUseDb() && await Databased(barcode))
            {
                BarcodeView? result = await Database(barcode);
                CacheIt(barcode, result);
                return result;
            }
            else if (!useDb)
            {
                logger.LogInformation("Fetching from db is turned off.");
            }
            else
            {
                logger.LogInformation("Item not found in database.");
            }
            return null;
        }

        public async Task<BardcodeInjestRequest?> FindItemInNetwork(string barcode, string barcodeType)
        {
            if (await GetUseApis())
            {
                var ingestRequest = await NetworkProviders(barcode, barcodeType);
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

        private void CacheIt(string barcode, BarcodeView result)
        {
            if (!useCache) return;

            var entry = cache.CreateEntry(barcode);
            entry.Value = result;
            entry.SetAbsoluteExpiration(DateTime.UtcNow.AddMinutes(60))
                .SetSlidingExpiration(TimeSpan.FromMinutes(5));

        }

        private async Task<BarcodeView> Database(string barcode)
        {
            var entity = await database.GetBarcode(barcode);
            var request = mapper.Map(entity!);
            // Get provider information if available
            var providerData = await database.GetBarcodeDataProvided(barcode);
            if (providerData != null)
            {
                request.ProviderType = providerData.ProviderType;
                request.ProviderJson = providerData.ProviderJson;
            }

            return request;
        }

        private Task<BarcodeView> Cache(string barcode)
        {
            return Task.FromResult(cache.Get<BarcodeView>(barcode));
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
