using Microsoft.Playwright;
using Microsoft.Playwright;
using TalosAI.Core.Runner;

namespace TalosAI.Automation.Pages
{
    /// <summary>
    /// Self-healing extension methods for PlaywrightBasePage.
    /// Provides automatic selector fallback strategies with multiple selectors.
    /// </summary>
    public static class SelfHealingExtensions
    {
        /// <summary>
        /// Find element with multiple fallback selectors.
        /// Tries each selector in Playwright until one succeeds.
        /// </summary>
        public static async Task<ILocator> FindWithFallbackAsync(
            this IPage page,
            string elementName,
            params string[] selectors)
        {
            if (selectors == null || selectors.Length == 0)
                throw new ArgumentException("At least one selector must be provided", nameof(selectors));

            var playwrightExceptions = new List<Exception>();

            // Try all selectors in Playwright
            for (int i = 0; i < selectors.Length; i++)
            {
                try
                {
                    var locator = page.Locator(selectors[i]);
                    await locator.WaitForAsync(new()
                    {
                        State = WaitForSelectorState.Visible,
                        Timeout = i == 0 ? 10000 : 5000 // Primary gets more time
                    });

                    if (i > 0)
                    {
                        Console.WriteLine(
                            $"[SELF-HEAL] ? '{elementName}' found with Playwright fallback #{i}: {selectors[i]}");
                        Console.WriteLine($"  ??  Primary selector may need updating: {selectors[0]}");
                    }

                    return locator;
                }
                catch (Exception ex)
                {
                    playwrightExceptions.Add(ex);
                    Console.WriteLine($"[SELF-HEAL] ? Playwright failed for '{elementName}' with selector: {selectors[i]}");
                }
            }

            // Throw comprehensive error if all selectors fail
            var attemptedSelectors = string.Join(", ", selectors);
            throw new TimeoutException(
                $"[SELF-HEAL] Failed to find '{elementName}' using any selector.\n" +
                $"  Attempted selectors: {attemptedSelectors}\n" +
                $"  Selectors tried: {selectors.Length}",
                playwrightExceptions.First());
        }

        /// <summary>
        /// Selector stability ranking helper.
        /// Returns selectors in order of stability (data-testid > aria-label > id > text > css).
        /// </summary>
        public static string[] CreateFallbackSelectors(
            string? dataTestId = null,
            string? ariaLabel = null,
            string? id = null,
            string? text = null,
            string? css = null)
        {
            var selectors = new List<string>();

            if (!string.IsNullOrEmpty(dataTestId))
                selectors.Add($"[data-testid='{dataTestId}']");

            if (!string.IsNullOrEmpty(ariaLabel))
                selectors.Add($"[aria-label='{ariaLabel}']");

            if (!string.IsNullOrEmpty(id))
                selectors.Add($"#{id}");

            if (!string.IsNullOrEmpty(text))
                selectors.Add($":text('{text}')");

            if (!string.IsNullOrEmpty(css))
                selectors.Add(css);

            return selectors.ToArray();
        }
    }
}
