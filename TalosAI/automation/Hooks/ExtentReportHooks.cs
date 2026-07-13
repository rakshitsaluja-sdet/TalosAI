using AventStack.ExtentReports;
using AventStack.ExtentReports.Reporter;
using AventStack.ExtentReports.Gherkin.Model;
using OpenQA.Selenium;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TechTalk.SpecFlow;

namespace TalosAI.Automation.Hooks
{
    /// <summary>
    /// ExtentReports integration for SpecFlow test automation
    /// Features:
    /// - Pass/Fail count by feature and tag
    /// - Execution time tracking
    /// - CI/CD compatible HTML reports
    /// - Screenshot on failure
    /// - API details capture
    /// - Test step logging
    /// </summary>
    [Binding]
    public class ExtentReportHooks
    {
        // Static instances shared across all scenarios
        private static ExtentReports _extent;
        private static string _reportPath;
        private static string _screenshotFolder;
        private static DateTime _testRunStartTime;
        private static Dictionary<string, int> _featurePassCount = new();
        private static Dictionary<string, int> _featureFailCount = new();
        private static Dictionary<string, int> _tagPassCount = new();
        private static Dictionary<string, int> _tagFailCount = new();

        // Instance variables per scenario
        private readonly ScenarioContext _scenarioContext;
        private readonly FeatureContext _featureContext;
        private readonly IWebDriver? _driver;
        private ExtentTest _scenario;
        private ExtentTest _currentStep;
        private DateTime _scenarioStartTime;

        public ExtentReportHooks(ScenarioContext scenarioContext, FeatureContext featureContext)
        {
            _scenarioContext = scenarioContext;
            _featureContext = featureContext;

            // Try to get driver if available (UI tests)
            try
            {
                _driver = scenarioContext.ScenarioContainer.Resolve<IWebDriver>();
            }
            catch
            {
                _driver = null; // API-only scenario
            }
        }

        /// <summary>
        /// Initialize ExtentReports before test run
        /// Creates report directory and configures reporter
        /// </summary>
        [BeforeTestRun(Order = -1000)]
        public static void InitializeExtentReport()
        {
            try
            {
                _testRunStartTime = DateTime.Now;
                
                // Create report directory
                var timestamp = _testRunStartTime.ToString("yyyyMMdd_HHmmss");
                var reportRoot = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "TestReports", "ExtentReports");
                var runFolder = Path.Combine(reportRoot, $"Run_{timestamp}");
                Directory.CreateDirectory(runFolder);
                
                _reportPath = Path.Combine(runFolder, "TestReport.html");
                _screenshotFolder = Path.Combine(runFolder, "Screenshots");
                Directory.CreateDirectory(_screenshotFolder);

                Console.WriteLine("=====================================");
                Console.WriteLine("[ExtentReports] Initializing Report");
                Console.WriteLine($"[ExtentReports] Report Path: {_reportPath}");
                Console.WriteLine($"[ExtentReports] Screenshots: {_screenshotFolder}");
                Console.WriteLine("=====================================");

                // Initialize ExtentReports with HTML reporter
                var htmlReporter = new ExtentSparkReporter(_reportPath);
                
                // Configure HTML reporter (v5.x configuration)
                htmlReporter.Config.Theme = AventStack.ExtentReports.Reporter.Config.Theme.Dark;
                htmlReporter.Config.DocumentTitle = "TalosAI Test Automation Report";
                htmlReporter.Config.ReportName = "Billing Rating Engine - Test Execution Report";
                htmlReporter.Config.Encoding = "UTF-8";
                htmlReporter.Config.TimeStampFormat = "MMM dd, yyyy HH:mm:ss";

                _extent = new ExtentReports();
                _extent.AttachReporter(htmlReporter);

                // System information
                _extent.AddSystemInfo("Environment", GetConfigValue("Environment", "DEV"));
                _extent.AddSystemInfo("Application", "TalosAI Test Automation Framework");
                _extent.AddSystemInfo("Browser", GetConfigValue("Browser", "EDGE"));
                _extent.AddSystemInfo("Test Framework", "SpecFlow + NUnit + .NET 8");
                _extent.AddSystemInfo("Base URL", GetConfigValue("ApiBaseUrl", "https://reqres.in"));
                _extent.AddSystemInfo("Test Run Date", _testRunStartTime.ToString("yyyy-MM-dd HH:mm:ss"));
                _extent.AddSystemInfo("Machine", Environment.MachineName);
                _extent.AddSystemInfo("User", Environment.UserName);
                _extent.AddSystemInfo("OS", Environment.OSVersion.ToString());
                _extent.AddSystemInfo(".NET Version", Environment.Version.ToString());

                Console.WriteLine("[ExtentReports] Report initialized successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ExtentReports] Initialization failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Create test node before each scenario
        /// </summary>
        [BeforeScenario(Order = -100)]
        public void BeforeScenario()
        {
            try
            {
                _scenarioStartTime = DateTime.Now;
                
                var featureName = _featureContext.FeatureInfo.Title;
                var scenarioName = _scenarioContext.ScenarioInfo.Title;
                
                // Create test for this scenario (feature grouping handled by ExtentReports automatically)
                _scenario = _extent.CreateTest(scenarioName, _featureContext.FeatureInfo.Description);
                
                // Add feature as category
                _scenario.AssignCategory(featureName);
                
                // Add tags as categories
                if (_scenarioContext.ScenarioInfo.Tags != null && _scenarioContext.ScenarioInfo.Tags.Length > 0)
                {
                    foreach (var tag in _scenarioContext.ScenarioInfo.Tags)
                    {
                        _scenario.AssignCategory(tag);
                    }
                }
                
                // Add author (can be extracted from tags or config)
                _scenario.AssignAuthor("TalosAI QA Team");
                
                // Add device (UI or API)
                if (_driver != null)
                {
                    _scenario.AssignDevice("Web Browser - UI");
                }
                else
                {
                    _scenario.AssignDevice("API");
                }

                Console.WriteLine($"[ExtentReports] Scenario started: {scenarioName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ExtentReports] BeforeScenario error: {ex.Message}");
            }
        }

        /// <summary>
        /// Log each step execution
        /// </summary>
        [AfterStep(Order = 100)]
        public void AfterStep()
        {
            try
            {
                var stepInfo = _scenarioContext.StepContext.StepInfo;
                var stepType = stepInfo.StepDefinitionType.ToString();
                var stepText = stepInfo.Text;
                var hasError = _scenarioContext.TestError != null;

                // Create step node
                var stepName = $"{stepType} {stepText}";
                
                if (hasError)
                {
                    _currentStep = _scenario.CreateNode(stepName).Fail(_scenarioContext.TestError);
                    
                    // Log error details
                    _currentStep.Fail($"<pre>{_scenarioContext.TestError.Message}</pre>");
                    
                    if (!string.IsNullOrEmpty(_scenarioContext.TestError.StackTrace))
                    {
                        _currentStep.Fail($"<details><summary>Stack Trace</summary><pre>{_scenarioContext.TestError.StackTrace}</pre></details>");
                    }

                    // Capture screenshot on failure (UI tests only)
                    if (_driver != null)
                    {
                        try
                        {
                            var screenshot = (_driver as ITakesScreenshot)?.GetScreenshot();
                            if (screenshot != null)
                            {
                                var fileName = $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid()}.png";
                                var filePath = Path.Combine(_screenshotFolder, fileName);
                                screenshot.SaveAsFile(filePath);
                                
                                // Attach screenshot to report with relative path
                                var relativePath = Path.Combine("Screenshots", fileName);
                                _currentStep.Fail("Screenshot on failure:", MediaEntityBuilder.CreateScreenCaptureFromPath(relativePath).Build());
                                
                                Console.WriteLine($"[ExtentReports] Screenshot captured: {fileName}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[ExtentReports] Screenshot capture failed: {ex.Message}");
                        }
                    }
                }
                else
                {
                    _currentStep = _scenario.CreateNode(stepName).Pass("Step executed successfully");
                }

                // Attach API details if present in ScenarioContext
                if (_scenarioContext.ContainsKey("API_REQUEST_URL"))
                {
                    try
                    {
                        var requestUrl = _scenarioContext.Get<string>("API_REQUEST_URL");
                        var statusCode = _scenarioContext.ContainsKey("API_STATUS_CODE")
                            ? _scenarioContext.Get<int>("API_STATUS_CODE").ToString()
                            : "N/A";
                        var responseTime = _scenarioContext.ContainsKey("API_RESPONSE_TIME")
                            ? _scenarioContext.Get<double>("API_RESPONSE_TIME").ToString("F2")
                            : "N/A";
                        var responseBody = _scenarioContext.ContainsKey("API_RESPONSE_BODY")
                            ? _scenarioContext.Get<string>("API_RESPONSE_BODY")
                            : "N/A";

                        var apiDetails = $@"
                            <div class='step-details'>
                                <h4>API Request Details</h4>
                                <p><strong>URL:</strong> {requestUrl}</p>
                                <p><strong>Status Code:</strong> {statusCode}</p>
                                <p><strong>Response Time:</strong> {responseTime} ms</p>
                                <details>
                                    <summary><strong>Response Body</strong></summary>
                                    <pre>{responseBody}</pre>
                                </details>
                            </div>";

                        _currentStep.Info(apiDetails);
                        Console.WriteLine("[ExtentReports] API details attached to step");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ExtentReports] API details attachment failed: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ExtentReports] AfterStep error: {ex.Message}");
            }
        }

        /// <summary>
        /// Finalize scenario results
        /// </summary>
        [AfterScenario(Order = 100)]
        public void AfterScenario()
        {
            try
            {
                var scenarioEndTime = DateTime.Now;
                var executionTime = (scenarioEndTime - _scenarioStartTime).TotalSeconds;
                
                var featureName = _featureContext.FeatureInfo.Title;
                var hasError = _scenarioContext.TestError != null;

                // Attach Playwright screenshot if available
                if (hasError && _scenarioContext.ContainsKey("PLAYWRIGHT_SCREENSHOT_PATH"))
                {
                    try
                    {
                        var screenshotPath = _scenarioContext.Get<string>("PLAYWRIGHT_SCREENSHOT_PATH");
                        _scenario.Fail("Test failed. Screenshot attached:", 
                            MediaEntityBuilder.CreateScreenCaptureFromPath(screenshotPath).Build());
                        Console.WriteLine($"[ExtentReports] Playwright screenshot attached to report");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ExtentReports] Failed to attach Playwright screenshot: {ex.Message}");
                    }
                }

                // Update pass/fail counts for features
                if (!_featurePassCount.ContainsKey(featureName))
                {
                    _featurePassCount[featureName] = 0;
                    _featureFailCount[featureName] = 0;
                }

                if (hasError)
                {
                    _featureFailCount[featureName]++;
                }
                else
                {
                    _featurePassCount[featureName]++;
                }

                // Update pass/fail counts for tags
                if (_scenarioContext.ScenarioInfo.Tags != null)
                {
                    foreach (var tag in _scenarioContext.ScenarioInfo.Tags)
                    {
                        if (!_tagPassCount.ContainsKey(tag))
                        {
                            _tagPassCount[tag] = 0;
                            _tagFailCount[tag] = 0;
                        }

                        if (hasError)
                        {
                            _tagFailCount[tag]++;
                        }
                        else
                        {
                            _tagPassCount[tag]++;
                        }
                    }
                }

                // Add execution time to scenario
                _scenario.Info($"<strong>Execution Time:</strong> {executionTime:F2} seconds");

                Console.WriteLine($"[ExtentReports] Scenario completed: {_scenarioContext.ScenarioInfo.Title} ({executionTime:F2}s)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ExtentReports] AfterScenario error: {ex.Message}");
            }
        }

        /// <summary>
        /// Finalize report after all tests
        /// </summary>
        [AfterTestRun(Order = 1000)]
        public static void FinalizeExtentReport()
        {
            try
            {
                if (_extent == null)
                {
                    Console.WriteLine("[ExtentReports] Report not initialized, skipping finalization");
                    return;
                }

                var testRunEndTime = DateTime.Now;
                var totalExecutionTime = (testRunEndTime - _testRunStartTime).TotalMinutes;

                Console.WriteLine("=====================================");
                Console.WriteLine("[ExtentReports] Finalizing Report");
                Console.WriteLine($"[ExtentReports] Total Execution Time: {totalExecutionTime:F2} minutes");

                // Print feature summary
                Console.WriteLine("[ExtentReports] Feature Summary:");
                foreach (var feature in _featurePassCount.Keys.Union(_featureFailCount.Keys))
                {
                    var passed = _featurePassCount.ContainsKey(feature) ? _featurePassCount[feature] : 0;
                    var failed = _featureFailCount.ContainsKey(feature) ? _featureFailCount[feature] : 0;
                    var total = passed + failed;
                    Console.WriteLine($"  {feature}: {passed}/{total} passed ({failed} failed)");
                }

                // Print tag summary
                if (_tagPassCount.Count > 0 || _tagFailCount.Count > 0)
                {
                    Console.WriteLine("[ExtentReports] Tag Summary:");
                    foreach (var tag in _tagPassCount.Keys.Union(_tagFailCount.Keys))
                    {
                        var passed = _tagPassCount.ContainsKey(tag) ? _tagPassCount[tag] : 0;
                        var failed = _tagFailCount.ContainsKey(tag) ? _tagFailCount[tag] : 0;
                        var total = passed + failed;
                        Console.WriteLine($"  {tag}: {passed}/{total} passed ({failed} failed)");
                    }
                }

                // Flush and save report
                _extent.Flush();

                Console.WriteLine($"[ExtentReports] Report saved: {_reportPath}");
                Console.WriteLine($"[ExtentReports] Screenshots folder: {_screenshotFolder}");
                Console.WriteLine("=====================================");

                // Auto-open report if configured
                var autoOpen = GetConfigValue("ExtentReportAutoOpen", "false");
                if (autoOpen.Equals("true", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = _reportPath,
                            UseShellExecute = true
                        });
                        Console.WriteLine("[ExtentReports] Report opened in browser");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ExtentReports] Failed to open report: {ex.Message}");
                    }
                }

                // Copy to "Latest" folder for CI/CD
                try
                {
                    var latestFolder = Path.Combine(Path.GetDirectoryName(_reportPath), "..", "Latest");
                    Directory.CreateDirectory(latestFolder);
                    
                    var latestReportPath = Path.Combine(latestFolder, "TestReport.html");
                    File.Copy(_reportPath, latestReportPath, true);
                    
                    // Copy screenshots folder
                    var latestScreenshotsFolder = Path.Combine(latestFolder, "Screenshots");
                    if (Directory.Exists(latestScreenshotsFolder))
                    {
                        Directory.Delete(latestScreenshotsFolder, true);
                    }
                    CopyDirectory(_screenshotFolder, latestScreenshotsFolder);
                    
                    Console.WriteLine($"[ExtentReports] Latest report: {latestReportPath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ExtentReports] Failed to copy to Latest folder: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ExtentReports] Finalization error: {ex.Message}");
            }
        }

        #region Helper Methods

        /// <summary>
        /// Get configuration value from config.properties
        /// </summary>
        private static string GetConfigValue(string key, string defaultValue)
        {
            try
            {
                var baseDir = AppContext.BaseDirectory;
                var projectRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".."));
                var configPath = Path.Combine(projectRoot, "automation", "config.properties");
                
                if (File.Exists(configPath))
                {
                    var config = TalosAI.core.Utils.BaseTest.ReadPropertiesFile(configPath);
                    return config.GetValueOrDefault(key, defaultValue);
                }
            }
            catch
            {
                // Ignore errors, return default
            }
            
            return defaultValue;
        }

        /// <summary>
        /// Copy directory recursively
        /// </summary>
        private static void CopyDirectory(string sourceDir, string destDir)
        {
            if (!Directory.Exists(sourceDir)) return;
            
            Directory.CreateDirectory(destDir);
            
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var fileName = Path.GetFileName(file);
                var destFile = Path.Combine(destDir, fileName);
                File.Copy(file, destFile, true);
            }
            
            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                var dirName = Path.GetFileName(dir);
                var destSubDir = Path.Combine(destDir, dirName);
                CopyDirectory(dir, destSubDir);
            }
        }

        #endregion
    }
}
