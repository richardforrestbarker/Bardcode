using Bardcoded.Data;
using Bardcoded.Shaded.Microsoft.AspNetCore.Mvc;
using Bardcoded.Wasm.Clients;
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
            

            builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

            builder.Services.AddScoped<CachedBarcodeLocalStorage>();
            builder.Services.AddScoped<CreateBarcodeLocalStorage>();
            builder.Services.AddSingleton(builder.Configuration.GetRequiredSection("BardcodedApiConfig").Get<BardcodedApiConfiguration>() ?? new BardcodedApiConfiguration());
            builder.Services.AddSingleton<IFeatureManager>(builder.Configuration.GetRequiredSection("Application").Get<MyFeatureManager>() ?? new MyFeatureManager());

            builder.Services.AddScoped<BardcodeApiHttpClient>();
            
            builder.RootComponents.Add<App>("#app");
            builder.RootComponents.Add<HeadOutlet>("head::after");
            
            await builder.Build().RunAsync();
        }
    }
}
