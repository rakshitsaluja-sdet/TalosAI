using BoDi;
using TalosAI.Core;
using TalosAI.Core.Runner;
using TechTalk.SpecFlow;

namespace TalosAI.Automation.Hooks
{
    /// <summary>
    /// Reqnroll hooks for managing Playwright lifecycle.
    /// Playwright is enabled via @playwright tag.
    /// </summary>
    [Binding]
    public sealed class PlaywrightHooks
    {
        private readonly IObjectContainer _container;
        private readonly ScenarioContext _scenarioContext;

        public PlaywrightHooks(
            IObjectContainer container,
            ScenarioContext scenarioContext)
        {
            _container = container;
            _scenarioContext = scenarioContext;
        }

        [BeforeScenario("@playwright")]
        public async Task BeforePlaywrightScenario()
        {
            var driver = PlaywrightDriver.Instance;

            // Read config for browser and headless settings
            var props = TalosAI.core.Utils.BaseTest.ReadConfigWithFallback();
            var browserPref = props.GetValueOrDefault("Browser", "EDGE").ToUpperInvariant();
            var headlessSetting = props.GetValueOrDefault("Headless", "false");

            // Detect if running in CI/CD
            bool isCI = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")) ||
                        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TF_BUILD")) ||
                        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")) ||
                        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AGENT_ID"));

            // Headless: Use config setting locally, force true in CI
            bool headless = isCI || headlessSetting.Equals("true", StringComparison.OrdinalIgnoreCase);

            // Map config browser to Playwright browser
            string playwrightBrowser = browserPref switch
            {
                "EDGE" => "chromium",      // Edge uses Chromium engine
                "CHROME" => "chromium",
                "FIREFOX" => "firefox",
                "WEBKIT" => "webkit",
                _ => "chromium"
            };

            Console.WriteLine($"[PlaywrightHooks] Launching Playwright with browser: {playwrightBrowser} (headless: {headless})");

            await driver.LaunchAsync(headless: headless, browser: playwrightBrowser);

            // Register for DI usage in steps/pages
            _container.RegisterInstanceAs(driver);
        }

        [AfterScenario("@playwright")]
        public async Task AfterPlaywrightScenario()
        {
            if (_scenarioContext.TestError != null &&
                _container.IsRegistered<PlaywrightDriver>())
            {
                var driver = _container.Resolve<PlaywrightDriver>();

                if (driver != null && driver.Page != null)
                {
                    try
                    {
                        // Create both screenshot folders
                        Directory.CreateDirectory("playwright-failures");

                        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                        var safeName = string.Concat(
                            _scenarioContext.ScenarioInfo.Title
                                .Split(Path.GetInvalidFileNameChars())
                        );

                        // Save to playwright-failures (for test results folder)
                        var testResultsPath = Path.Combine(
                            "playwright-failures",
                            $"{safeName}-{timestamp}.png");

                        await driver.Page.ScreenshotAsync(
                            new() { Path = testResultsPath });

                        // Also save to ExtentReports Screenshots folder (for HTML report)
                        var reportRoot = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "TestReports", "ExtentReports");
                        var runFolders = Directory.GetDirectories(reportRoot, "Run_*")
                            .OrderByDescending(d => Directory.GetCreationTime(d))
                            .FirstOrDefault();

                        if (runFolders != null)
                        {
                            var extentScreenshotFolder = Path.Combine(runFolders, "Screenshots");
                            Directory.CreateDirectory(extentScreenshotFolder);

                            var extentScreenshotFileName = $"{safeName}_{timestamp}.png";
                            var extentScreenshotPath = Path.Combine(extentScreenshotFolder, extentScreenshotFileName);

                            await driver.Page.ScreenshotAsync(
                                new() { Path = extentScreenshotPath });

                            // Store screenshot path in ScenarioContext for ExtentReports to use
                            _scenarioContext.Add("PLAYWRIGHT_SCREENSHOT_PATH", Path.Combine("Screenshots", extentScreenshotFileName));

                            Console.WriteLine($"[PlaywrightHooks] Screenshot saved for ExtentReports: {extentScreenshotFileName}");
                        }

                        Console.WriteLine($"[PlaywrightHooks] Screenshot captured: {testResultsPath}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[PlaywrightHooks] Screenshot capture failed: {ex.Message}");
                        // Do NOT fail scenario due to reporting
                    }
                }
            }

            // Clean up Playwright
            if (_container.IsRegistered<PlaywrightDriver>())
            {
                var driver = _container.Resolve<PlaywrightDriver>();
                await driver.DisposeAsync();
            }
        }
    }
}