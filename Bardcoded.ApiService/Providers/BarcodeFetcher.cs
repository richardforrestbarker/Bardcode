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
        private readonly ApiProviderConfiguration[] configs;
        private readonly MemoryCache cache;
        private readonly IFeatureManager features;
        private readonly IBarcodeDataContext database;
        private readonly IOMapper mapper;
        private readonly ILogger<BarcodeFetcher> logger;
        private bool useCache;
        private bool useDb;
        private bool useApis;

        public BarcodeFetcher(List<ApiProviderConfiguration> configs, MemoryCache cache, IFeatureManager features, IBarcodeDataContext database, ILoggerFactory factory)
        {
            this.configs = configs.ToArray();
            this.cache = cache;
            this.features = features;
            this.database = database;
            this.mapper = new IOMapper();
            this.logger = factory.CreateLogger<BarcodeFetcher>();
        }

        public async Task<BarcodeView?> FindItem(string barcode, string barcodeType)
        {
            BarcodeView result;
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
                result = await NetworkProviders(barcode, barcodeType);
                if (result != null) StoreAndCacheIt(barcode, result);
                return result;
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

        private async void CacheIt(string barcode, BarcodeView result)
        {
            if (!useCache) return;
            var entry = cache.CreateEntry(barcode);
            entry.Value = result;
            entry.SetAbsoluteExpiration(DateTime.UtcNow.AddMinutes(60))
                .SetSlidingExpiration(TimeSpan.FromMinutes(5));
        }

        private void StoreAndCacheIt(string barcode, BarcodeView result)
        {
            if (useCache) CacheIt(barcode, result);
            if (useDb) StoreIt(barcode, new BardcodeInjestRequest()
            {
                Bard = result.Code,
                Base64Image = result.ImageAsBase64,
                Description = result.Description,
                ImageType = result.ImageType,
                Name = result.Name,
                Source = "",
                WeightVolume = "",
            });
        }

        private void StoreIt(string barcode, BardcodeInjestRequest request)
        {
            var entity = mapper.Map(request);
            database.InsertBarcode(entity);
        }

        private async Task<BarcodeView> Database(string barcode)
        {
            IOMapper mapper = new IOMapper();
            var entity = await database.GetBarcode(barcode);
            return mapper.Map(entity);
        }

        private Task<BarcodeView> Cache(string barcode)
        {
            return Task.FromResult(cache.Get<BarcodeView>(barcode));
        }

        private async Task<BarcodeView?> NetworkProviders(string barcode, string barcodeType)
        {
            logger.LogInformation($"Fetching {barcode} from network providers.");
            foreach (var provider in configs)
            {
                if(!provider.IsBarcodeTypeAllowed(barcodeType))
                {
                    logger.LogInformation($"Skipping {barcode} for provider {provider.Type} due to barcode type restrictions.");
                    continue;
                }
                try
                {
                    if (provider.Type.Equals(nameof(UpcDatabaseApiProvider)))
                    {
                        logger.LogTrace("Using UpcDatabaseApiProvider");
                        var client = await provider.GetHttpClient();
                        if (await provider.IsOverRates())
                        {
                            logger.LogWarning($"Skipping {provider.Type} for {barcode} due to rate limiting.");
                            continue;
                        }
                        var response = await client.GetAsync(provider.Path.Replace("{barcode}", barcode));
                        if (await provider.IsResponseKosher(response))
                        {
                            logger.LogInformation($"Successfully fetched {barcode} from {provider.Type}.");
                            return await provider.Translate(response);
                        }
                    }
                }
                catch (Exception e)
                {
                    logger.LogError($"Caught and ignoring {e.GetType()} trying to get {barcode} from {provider.Type}: {e.Message}");
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
