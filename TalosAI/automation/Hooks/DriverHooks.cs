using BoDi;
using NUnit.Framework;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Edge;
using TechTalk.SpecFlow;
using System;
using System.IO;
using System.Linq;                   // for tags.Any(...)
using BoDi;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Edge;
using TechTalk.SpecFlow;

// Optional: only if you run SpecFlow with the NUnit runner and want to attach screenshots
using NUnit.Framework;

namespace TalosAI.Automation.Hooks
{
    [Binding]
    public class DriverHooks
    {
        private readonly IObjectContainer _container;
        private ScenarioContext? _scenarioContext;
        private IWebDriver? _driver;

        // Screenshots: <bin>/<config>/<tfm>/test-results/screenshots
        private static readonly string ArtifactsScreenshotsDir =
            Path.Combine(AppContext.BaseDirectory, "test-results", "screenshots");

        public DriverHooks(IObjectContainer container)
        {
            _container = container;
        }

        // Only start driver for UI scenarios, exclude API-only and Playwright scenarios
        [BeforeScenario(Order = 0)]
        public void StartDriver(ScenarioContext scenarioContext)
        {
            _scenarioContext = scenarioContext;   // store for later hooks

            var tags = scenarioContext.ScenarioInfo.Tags ?? Array.Empty<string>();

            // Skip Selenium driver if scenario uses Playwright
            if (tags.Contains("playwright"))
            {
                Console.WriteLine("[@playwright] Scenario uses Playwright - skipping Selenium driver initialization.");
                return;
            }

            // If scenario has @api tag but NO UI-related tags, skip driver initialization
            if (tags.Contains("api") && !RequiresUI(tags))
            {
                Console.WriteLine("[@api] API-only scenario detected. Skipping browser initialization.");
                return;
            }

            // Read browser prefs from the same config used by BaseTest
            var baseDir = AppContext.BaseDirectory;
            var projectRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".."));

            var props = TalosAI.core.Utils.BaseTest.ReadConfigWithFallback();

            var configuredBaseUrl = props.GetValueOrDefault("BaseUrl");
            if (!string.IsNullOrWhiteSpace(configuredBaseUrl))
                TalosAI.core.Utils.BaseTest.BaseUrl = configuredBaseUrl;

            var browserPref = props.GetValueOrDefault("Browser", "EDGE");
            var headless = props.GetValueOrDefault("Headless", "true");

            Directory.CreateDirectory(ArtifactsScreenshotsDir);

            if (browserPref.Equals("EDGE", StringComparison.OrdinalIgnoreCase))
            {
                var options = new EdgeOptions();
                options.AddArgument("start-maximized");
                options.AddArgument("--disable-infobars");
                // Helpful for CI; comment out locally if you want headed
                if (headless.Equals("True", StringComparison.OrdinalIgnoreCase))
                {
                    options.AddArgument("--headless=new");
                }
                    options.AddArgument("--no-sandbox");
                options.AddArgument("--disable-dev-shm-usage");

                _driver = new EdgeDriver(options);
            }
            else // CHROME default
            {
                var options = new ChromeOptions();
                options.AddArgument("--start-maximized");
                    options.AddArgument("--disable-infobars");
                // Helpful for CI; comment out locally if you want headed
                if (headless.Equals("True", StringComparison.OrdinalIgnoreCase))
                {
                    options.AddArgument("--headless=new");
                }
                options.AddArgument("--no-sandbox");
                    options.AddArgument("--disable-dev-shm-usage");

                var service = ChromeDriverService.CreateDefaultService();
                service.HideCommandPromptWindow = true;

                _driver = new ChromeDriver(service, options, TimeSpan.FromSeconds(60));
            }

            _container.RegisterInstanceAs<IWebDriver>(_driver);
            Console.WriteLine("Browser initialized for UI testing.");
        }

        // Take screenshot at the exact failing step
        [AfterStep]
        public void TakeScreenshotOnStepFailure()
        {
            if (_driver == null || _scenarioContext == null) return;

            if (_scenarioContext.TestError != null)
            {
                var stepText = _scenarioContext.StepContext?.StepInfo?.Text ?? "unknown-step";
                TryCaptureScreenshot($"STEP_FAIL_{Sanitize(stepText)}");
            }
        }

        // Fallback: capture one more if the scenario failed
        [AfterScenario(Order = 0)]
        public void TakeScreenshotOnScenarioFailure()
        {
            if (_driver == null || _scenarioContext == null) return;

            if (_scenarioContext.TestError != null)
            {
                TryCaptureScreenshot("SCENARIO_FAIL");
            }
        }

        [AfterScenario(Order = 100)]
        public void StopDriver()
        {
            if (_driver != null)
            {
                try { _driver.Quit(); } catch { /* ignore */ }
            }
        }

        private bool RequiresUI(string[] tags)
        {
            string[] uiTags = { "ui", "login", "dashboard", "smoke", "tiles", "financial-project",
                                "customer-order", "invoice-reco", "account-selection" };
            return tags.Any(tag => uiTags.Contains(tag.ToLowerInvariant()));
        }

        private void TryCaptureScreenshot(string suffix)
        {
            try
            {
                if (_driver is not ITakesScreenshot taker) return;

                Directory.CreateDirectory(ArtifactsScreenshotsDir);

                var scenarioName = Sanitize(_scenarioContext?.ScenarioInfo?.Title ?? "scenario");
                var stepPart = suffix; // already includes step or SCENARIO_FAIL
                string TrimTo(string s, int max) => s.Length <= max ? s : s.Substring(0, max);

                // Keep names shorter to avoid MAX_PATH issues
                scenarioName = TrimTo(scenarioName, 80);
                stepPart = TrimTo(stepPart, 80);

                var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmssfff");
                var fileName = $"{scenarioName}_{stepPart}_{timestamp}.png";
                var fullPath = Path.Combine(ArtifactsScreenshotsDir, fileName);

                // Write bytes (stable)
                var shot = ((ITakesScreenshot)_driver).GetScreenshot();
                File.WriteAllBytes(fullPath, shot.AsByteArray);

                // If you prefer SaveAsFile with enum, uncomment this line:
                // shot.SaveAsFile(fullPath, OpenQA.Selenium.ScreenshotImageFormat.Png);

                Console.WriteLine($"Saved screenshot: {fullPath}");

                // Attach to NUnit results if available (safe no-op if not using NUnit)
                try { TestContext.AddTestAttachment(fullPath, "Failure screenshot"); } catch { }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARN] Failed to capture screenshot: {ex.Message}");
            }
        }

        private static string Sanitize(string input)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var cleaned = string.Join("_", input.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
            return string.IsNullOrWhiteSpace(cleaned) ? "unnamed" : cleaned;
        }
    }
}