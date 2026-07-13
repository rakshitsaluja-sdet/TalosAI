namespace McpBridge.Tools
{
    /// <summary>
    /// Self-healing service for MCPBridge.
    /// Analyzes selector failures and suggests alternatives.
    /// </summary>
    public class SelectorHealingService
    {
        /// <summary>
        /// Analyzes a failed selector and returns alternative selector strategies.
        /// Uses Playwright's semantic selector hierarchy.
        /// </summary>
        public static List<string> GenerateFallbackSelectors(string originalSelector, string? context = null)
        {
            var fallbacks = new List<string> { originalSelector };

            // Extract potential alternative selectors based on the original
            if (originalSelector.StartsWith("[data-testid"))
            {
                // If data-testid fails, try aria-label, id, then text
                var testId = ExtractAttribute(originalSelector, "data-testid");
                if (!string.IsNullOrEmpty(testId))
                {
                    fallbacks.Add($"[aria-label*='{testId}']");
                    fallbacks.Add($"#{testId}");
                    fallbacks.Add($":text('{testId}')");
                }
            }
            else if (originalSelector.StartsWith("#"))
            {
                // If ID fails, try data-testid, class, or tag
                var id = originalSelector.TrimStart('#');
                fallbacks.Add($"[data-testid='{id}']");
                fallbacks.Add($".{id}");
                fallbacks.Add($"[name='{id}']");
            }
            else if (originalSelector.Contains("button"))
            {
                // Button-specific fallbacks
                fallbacks.Add("button[type='submit']");
                fallbacks.Add("input[type='submit']");
                fallbacks.Add("[role='button']");
            }
            else if (originalSelector.Contains("input"))
            {
                // Input-specific fallbacks
                var typeMatch = System.Text.RegularExpressions.Regex.Match(originalSelector, @"type='(\w+)'");
                if (typeMatch.Success)
                {
                    var inputType = typeMatch.Groups[1].Value;
                    fallbacks.Add($"input[type='{inputType}']");
                    fallbacks.Add($"[name*='{inputType}']");
                }
            }

            return fallbacks;
        }

        /// <summary>
        /// Logs self-healing attempts for monitoring and alerting.
        /// </summary>
        public static void LogSelfHealingAttempt(
            string toolName,
            string originalSelector,
            string fallbackSelector,
            bool success)
        {
            var status = success ? "SUCCESS" : "FAILED";
            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
            
            Console.WriteLine(
                $"[SELF-HEAL] {timestamp} | {status} | Tool: {toolName}\n" +
                $"  Original: {originalSelector}\n" +
                $"  Fallback: {fallbackSelector}");

            // TODO: Send to monitoring system (Azure Application Insights, ExtentReports, etc.)
        }

        private static string? ExtractAttribute(string selector, string attributeName)
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                selector, 
                $@"{attributeName}='([^']+)'");
            return match.Success ? match.Groups[1].Value : null;
        }
    }
}
