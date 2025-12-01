using Bardcoded.Data.Ocr;
using Bardcoded.Data;
using Bardcoded.Shaded.Microsoft.AspNetCore.Mvc;
using Bardcoded.Wasm.Clients;
using Bardcoded.Wasm.Pages;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;
using System.Text.Json;

namespace Bardcoded.Wasm
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);


            builder.Services.AddScoped<ClientErrorHandlingHttpMessageHandler>();
            builder.Services.AddHttpClient("default", (serves, client) =>
            {
                client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact;
                client.Timeout = TimeSpan.FromSeconds(30);
                client.MaxResponseContentBufferSize = 1024 * 100;
                if (!Uri.TryCreate(builder.HostEnvironment.BaseAddress, UriKind.Absolute, out var baseAddress))
                {
                    Console.WriteLine($"Invalid API URL: {builder.HostEnvironment.BaseAddress}");
                    throw new InvalidOperationException($"Invalid API URL: {builder.HostEnvironment.BaseAddress}");
                }
                client.BaseAddress = baseAddress;
                Console.WriteLine($"HTTP Client Base Address: {client.BaseAddress}");
            }).AddHttpMessageHandler<ClientErrorHandlingHttpMessageHandler>();
            
            builder.Services.AddHttpClient("document-processing", (serves, client) =>
            {
                client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact;
                client.Timeout = TimeSpan.FromSeconds(180);
                client.MaxResponseContentBufferSize = 1024 * 100;
                if (!Uri.TryCreate("https://localhost:7415", UriKind.Absolute, out var baseAddress))
                {
                    Console.WriteLine($"Invalid API URL: {builder.HostEnvironment.BaseAddress}");
                    throw new InvalidOperationException($"Invalid API URL: {builder.HostEnvironment.BaseAddress}");
                }
                client.BaseAddress = baseAddress;
                Console.WriteLine($"HTTP Client Base Address: {client.BaseAddress}");
            }).AddHttpMessageHandler<ClientErrorHandlingHttpMessageHandler>();


            builder.Services.AddScoped<IDocumentProcessor, ClientSideDocumentProcessor>();

            builder.Services.AddTransient<JsonSerializerOptions>(sp => new JsonSerializerOptions()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false,
                IncludeFields = true,
                AllowTrailingCommas = true,
                PropertyNameCaseInsensitive = true,
                AllowOutOfOrderMetadataProperties = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            });
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
