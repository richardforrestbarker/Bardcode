using Bardcoded.Data;
using Bardcoded.Shaded.Microsoft.AspNetCore.Mvc;
using Bardcoded.Wasm.Clients;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Bardcoded.Wasm
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            
            // Configure HttpClient to include credentials (cookies)
            builder.Services.AddScoped(sp => 
            {
                var client = new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) };
                return client;
            });

            builder.Services.AddScoped<CachedBarcodeLocalStorage>();
            builder.Services.AddScoped<CreateBarcodeLocalStorage>();
            builder.Services.AddSingleton(builder.Configuration.GetRequiredSection("BardcodedApiConfig").Get<BardcodedApiConfiguration>() ?? new BardcodedApiConfiguration());
            builder.Services.AddSingleton<IFeatureManager>(builder.Configuration.GetRequiredSection("Application").Get<MyFeatureManager>() ?? new MyFeatureManager());

            builder.Services.AddScoped<BardcodeApiHttpClient>();
            
            // Add authentication services
            builder.Services.AddAuthorizationCore();
            builder.Services.AddCascadingAuthenticationState();
            builder.Services.AddSingleton<AuthenticationStateProvider, PersistentAuthenticationStateProvider>();
            
            builder.RootComponents.Add<App>("#app");
            builder.RootComponents.Add<HeadOutlet>("head::after");
            
            await builder.Build().RunAsync();
        }
    }
}
