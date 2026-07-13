using Microsoft.Playwright;

namespace TalosAI.Core.Runner
{
    /// <summary>
    /// Singleton Playwright driver shared across all steps
    /// in a SpecFlow scenario. Mirrors your existing
    /// SeleniumDriver pattern in core\runner.
    /// </summary>
    public sealed class PlaywrightDriver : IAsyncDisposable
    {
        private static PlaywrightDriver? _instance;
        private static readonly SemaphoreSlim _lock = new(1, 1);

        private IPlaywright? _playwright;
        private IBrowser? _browser;
        private IBrowserContext? _context;
        private IPage? _page;


        public IPage Page => _page
            ?? throw new InvalidOperationException(
                "Browser not launched. Call LaunchAsync first.");

        public static PlaywrightDriver Instance
        {
            get
            {
                _instance ??= new PlaywrightDriver();
                return _instance;
            }
        }

        private PlaywrightDriver() { }

        public async Task LaunchAsync(
            bool headless = false,
            string browser = "chromium")
        {
            _playwright = await Playwright.CreateAsync();

            // Launch browser with args to start maximized (for Chromium)
            var launchOptions = new BrowserTypeLaunchOptions
            {
                Headless = headless,
                SlowMo = 50
            };

            // Add args to start maximized (Chromium/Edge only)
            if (!headless && (browser.ToLower() == "chromium" || browser.ToLower() == "edge"))
            {
                launchOptions.Args = new[] { "--start-maximized" };
            }

            _browser = browser.ToLower() switch
            {
                "firefox" => await _playwright.Firefox
                    .LaunchAsync(new() { Headless = headless }),
                "webkit" => await _playwright.Webkit
                    .LaunchAsync(new() { Headless = headless }),
                _ => await _playwright.Chromium.LaunchAsync(launchOptions)
            };

            // For maximized browser, don't set viewport (let it use full screen)
            var contextOptions = new BrowserNewContextOptions();
            
            if (headless)
            {
                // Headless: Set fixed viewport
                contextOptions.ViewportSize = new() { Width = 1920, Height = 1080 };
                contextOptions.RecordVideoDir = "playwright-videos/";
            }
            else
            {
                // Non-headless: No viewport = use window size (maximized)
                contextOptions.ViewportSize = ViewportSize.NoViewport;
            }

            _context = await _browser.NewContextAsync(contextOptions);
            _page = await _context.NewPageAsync();
        }

        public async ValueTask DisposeAsync()
        {
            if (_page != null) await _page.CloseAsync();
            if (_context != null) await _context.CloseAsync();
            if (_browser != null) await _browser.CloseAsync();
            _playwright?.Dispose();

            _instance = null;
        }
    }
}