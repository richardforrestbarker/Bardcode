using Microsoft.Playwright;
using System.Threading.Tasks;

namespace Bardcoded.Tests
{
    public class PlaywrightFixture : IAsyncLifetime
    {
        public IPlaywright Playwright { get; private set; } = default!;
        public IBrowser Browser { get; private set; } = default!;
        public IBrowserContext BrowserContext { get; private set; } = default!;
        public IPage Page { get; private set; } = default!;
        public IAPIRequestContext ApiRequest { get; private set; } = default!;

        public async ValueTask InitializeAsync()
        {
            Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
            Browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
            BrowserContext = await Browser.NewContextAsync();
            Page = await BrowserContext.NewPageAsync();
            ApiRequest = await Playwright.APIRequest.NewContextAsync(new APIRequestNewContextOptions
            {
                BaseURL = System.Environment.GetEnvironmentVariable("API_BASE_URL") ?? "http://localhost:5001"
            });
        }

        public async ValueTask DisposeAsync()
        {
            await ApiRequest.DisposeAsync();
            await Page.CloseAsync();
            await BrowserContext.CloseAsync();
            await Browser.CloseAsync();
            Playwright.Dispose();
        }
    }
}
