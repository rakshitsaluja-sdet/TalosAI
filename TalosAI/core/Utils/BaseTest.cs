using System;

namespace TalosAI.core.Utils
{
    public class BaseTest
    {
        /// <summary>Target site for the UI scenarios. Overridable via config.properties ("BaseUrl").</summary>
        public static string BaseUrl = "https://www.saucedemo.com/";

        public static Dictionary<string, string> ReadPropertiesFile(string filePath)
        {
            var data = new Dictionary<string, string>();

            try
            {
                // Read all lines from the file
                foreach (var row in File.ReadAllLines(filePath))
                {
                    // Skip comments and empty lines
                    if (string.IsNullOrWhiteSpace(row) || row.Trim().StartsWith("#"))
                    {
                        continue;
                    }

                    // Split the line into key and value
                    var parts = row.Split('=');
                    if (parts.Length >= 2)
                    {
                        var key = parts[0].Trim();
                        // Join the rest of the parts in case the value contains an '=' sign
                        var value = string.Join("=", parts.Skip(1).ToArray()).Trim();

                        // Add to the dictionary
                        if (!string.IsNullOrEmpty(key))
                        {
                            data[key] = value;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error: The file '{filePath}' was not found.", e);
            }
            return data;
        }

        /// <summary>
        /// Reads configuration with fallback strategy:
        /// 1. config.properties (local overrides + secrets) - gitignored
        /// 2. config.template.properties (framework defaults) - committed to Git
        /// 3. Hardcoded defaults (if no files found)
        ///
        /// This approach ensures:
        /// - Local development: developers use config.properties with their credentials
        /// - CI/CD pipeline: uses config.template.properties + GitHub Secrets
        /// - Resilience: always has working defaults
        /// </summary>
        public static Dictionary<string, string> ReadConfigWithFallback()
        {
            var baseDir = AppContext.BaseDirectory;
            var projectRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".."));

            var configPath = Path.Combine(projectRoot, "automation", "config.properties");
            var templatePath = Path.Combine(projectRoot, "automation", "config.template.properties");

            // Priority 1: Local config.properties (developer overrides + secrets)
            if (File.Exists(configPath))
            {
                Console.WriteLine("[Config] Using config.properties");
                return ReadPropertiesFile(configPath);
            }

            // Priority 2: config.template.properties (defaults, safe for Git)
            if (File.Exists(templatePath))
            {
                Console.WriteLine("[Config] config.properties not found. Using config.template.properties (pipeline mode)");
                return ReadPropertiesFile(templatePath);
            }

            // Priority 3: Hardcoded defaults (safety net)
            Console.WriteLine("[Config] WARNING: No config files found. Using hardcoded defaults.");
            return new Dictionary<string, string>
            {
                { "BaseUrl", BaseUrl },
                { "Browser", "EDGE" },
                { "Headless", "true" },
                { "SeleniumFallback", "true" },
                { "SeleniumFallbackTimeout", "10" },
                { "PlaywrightTimeout", "30000" },
                { "PlaywrightNavigationTimeout", "30000" },
                { "TDMCleanupEnabled", "true" },
                { "CaptureScreenshotOnFailure", "true" }
            };
        }
    }
}
