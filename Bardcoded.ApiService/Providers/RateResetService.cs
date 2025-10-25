using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Bardcoded.ApiService.Providers
{
    public class RateResetService : BackgroundService
    {
        private readonly IEnumerable<ApiProviderConfiguration> _providerConfigs;
        private TimeSpan _period;

        public RateResetService(IServiceProvider serviceProvider)
        {
            // Assume provider configs are registered as singleton IEnumerable<ApiProviderConfiguration>
            _providerConfigs = serviceProvider.GetRequiredService<IEnumerable<ApiProviderConfiguration>>();
            _period = GetLowestPeriod(_providerConfigs);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                foreach (var config in _providerConfigs)
                {
                    if (config.RateLimit == null || config.Rate == null)
                        continue;

                    
                    if (config.Rate.NextReset <= DateTime.UtcNow)
                    {
                        config.Rate.Count = 0;
                        config.Rate.NextReset = DateTime.UtcNow.Add(config.RateLimit.TimeSpan);
                    }
                }
                await Task.Delay(_period, stoppingToken);
            }
        }

        private static TimeSpan GetLowestPeriod(IEnumerable<ApiProviderConfiguration> configs)
        {
            var periods = configs
                .Where(c => c.RateLimit != null)
                .Select(c => c.RateLimit.TimeSpan)
                .Where(ts => ts > TimeSpan.Zero)
                .ToList();
            return periods.Any() ? periods.Min() : TimeSpan.FromMinutes(1);
        }
    }
}
