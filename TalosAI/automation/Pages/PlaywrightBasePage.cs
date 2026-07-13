using TalosAI.Core.Runner;
using Microsoft.Playwright;
using System.Text.RegularExpressions;

namespace TalosAI.Automation.Pages
{
    /// <summary>
    /// Base class for all Playwright-based page objects.
    /// Mirrors Selenium BasePage pattern in TalosAI.
    /// Now includes self-healing capabilities via fallback selectors.
    /// </summary>
    public abstract class PlaywrightBasePage
    {
        protected readonly IPage Page;
        protected readonly PlaywrightDriver Driver;

        protected PlaywrightBasePage(PlaywrightDriver driver)
        {
            Driver = driver;
            Page = driver.Page
                ?? throw new InvalidOperationException("Playwright page is not initialized.");
        }

        // ── Navigation helpers ─────────────────────────────────────────

        protected async Task NavigateToAsync(string url)
        {
            await Page.GotoAsync(
                url,
                new() { WaitUntil = WaitUntilState.NetworkIdle });
        }

        // ── Self-Healing Input helpers ────────────────────────────────

        /// <summary>
        /// Fill input with self-healing fallback selectors.
        /// Tries each selector until one succeeds.
        /// </summary>
        protected async Task FillWithFallbackAsync(
            string elementName,
            string value,
            params string[] selectors)
        {
            var locator = await Page.FindWithFallbackAsync(elementName, selectors);
            await locator.ScrollIntoViewIfNeededAsync();
            await locator.ClearAsync();
            await locator.FillAsync(value);
        }

        /// <summary>
        /// Original Fill method (preserved for backward compatibility).
        /// </summary>
        protected async Task FillAsync(string selector, string value)
        {
            var locator = Page.Locator(selector);
            await locator.WaitForAsync(new() { State = WaitForSelectorState.Visible });
            await locator.ScrollIntoViewIfNeededAsync();
            await locator.ClearAsync();
            await locator.FillAsync(value);
        }

        protected async Task FillByLabelAsync(string label, string value)
        {
            await Page.GetByLabel(label, new() { Exact = false })
                .FillAsync(value);
        }

        protected async Task FillByPlaceholderAsync(string placeholder, string value)
        {
            await Page.GetByPlaceholder(placeholder, new() { Exact = false })
                .FillAsync(value);
        }

        // ── Self-Healing Click helpers ────────────────────────────────

        /// <summary>
        /// Click element with self-healing fallback selectors.
        /// Tries each selector until one succeeds.
        /// </summary>
        protected async Task ClickWithFallbackAsync(
            string elementName,
            params string[] selectors)
        {
            var locator = await Page.FindWithFallbackAsync(elementName, selectors);
            await locator.ScrollIntoViewIfNeededAsync();
            await locator.ClickAsync();
        }

        /// <summary>
        /// Double-click element with self-healing fallback selectors.
        /// </summary>
        protected async Task DoubleClickWithFallbackAsync(
            string elementName,
            params string[] selectors)
        {
            var locator = await Page.FindWithFallbackAsync(elementName, selectors);
            await locator.ScrollIntoViewIfNeededAsync();
            await locator.DblClickAsync();
        }

        /// <summary>
        /// Right-click element with self-healing fallback selectors.
        /// </summary>
        protected async Task RightClickWithFallbackAsync(
            string elementName,
            params string[] selectors)
        {
            var locator = await Page.FindWithFallbackAsync(elementName, selectors);
            await locator.ScrollIntoViewIfNeededAsync();
            await locator.ClickAsync(new() { Button = MouseButton.Right });
        }

        /// <summary>
        /// Hover over element with self-healing fallback selectors.
        /// </summary>
        protected async Task HoverWithFallbackAsync(
            string elementName,
            params string[] selectors)
        {
            var locator = await Page.FindWithFallbackAsync(elementName, selectors);
            await locator.ScrollIntoViewIfNeededAsync();
            await locator.HoverAsync();
        }

        /// <summary>
        /// Original Click method (preserved for backward compatibility).
        /// </summary>
        protected async Task ClickAsync(string selector)
        {
            var locator = Page.Locator(selector);
            await locator.WaitForAsync(new() { State = WaitForSelectorState.Visible });
            await locator.ScrollIntoViewIfNeededAsync();
            await locator.ClickAsync();
        }

        protected async Task ClickByTextAsync(string text)
        {
            await Page.GetByText(text, new() { Exact = false })
                .ClickAsync();
        }

        protected async Task ClickByRoleAsync(AriaRole role, string name)
        {
            await Page.GetByRole(role, new() { Name = name })
                .ClickAsync();
        }

        // ── Self-Healing Dropdown/Select helpers ──────────────────────

        /// <summary>
        /// Select option from dropdown with self-healing fallback selectors.
        /// </summary>
        protected async Task SelectOptionWithFallbackAsync(
            string elementName,
            string optionValue,
            params string[] selectors)
        {
            var locator = await Page.FindWithFallbackAsync(elementName, selectors);
            await locator.SelectOptionAsync(new[] { optionValue });
        }

        /// <summary>
        /// Select option by label with self-healing fallback selectors.
        /// </summary>
        protected async Task SelectOptionByLabelWithFallbackAsync(
            string elementName,
            string optionLabel,
            params string[] selectors)
        {
            var locator = await Page.FindWithFallbackAsync(elementName, selectors);
            await locator.SelectOptionAsync(new SelectOptionValue { Label = optionLabel });
        }

        // ── Self-Healing Checkbox/Radio helpers ───────────────────────

        /// <summary>
        /// Check checkbox with self-healing fallback selectors.
        /// </summary>
        protected async Task CheckWithFallbackAsync(
            string elementName,
            params string[] selectors)
        {
            var locator = await Page.FindWithFallbackAsync(elementName, selectors);
            await locator.CheckAsync();
        }

        /// <summary>
        /// Uncheck checkbox with self-healing fallback selectors.
        /// </summary>
        protected async Task UncheckWithFallbackAsync(
            string elementName,
            params string[] selectors)
        {
            var locator = await Page.FindWithFallbackAsync(elementName, selectors);
            await locator.UncheckAsync();
        }

        /// <summary>
        /// Set checkbox state with self-healing fallback selectors.
        /// </summary>
        protected async Task SetCheckedWithFallbackAsync(
            string elementName,
            bool isChecked,
            params string[] selectors)
        {
            var locator = await Page.FindWithFallbackAsync(elementName, selectors);
            await locator.SetCheckedAsync(isChecked);
        }

        // ── Self-Healing Text Retrieval helpers ───────────────────────

        /// <summary>
        /// Get text content with self-healing fallback selectors.
        /// </summary>
        protected async Task<string> GetTextWithFallbackAsync(
            string elementName,
            params string[] selectors)
        {
            var locator = await Page.FindWithFallbackAsync(elementName, selectors);
            return await locator.InnerTextAsync();
        }

        /// <summary>
        /// Get input value with self-healing fallback selectors.
        /// </summary>
        protected async Task<string> GetInputValueWithFallbackAsync(
            string elementName,
            params string[] selectors)
        {
            var locator = await Page.FindWithFallbackAsync(elementName, selectors);
            return await locator.InputValueAsync();
        }

        /// <summary>
        /// Get attribute value with self-healing fallback selectors.
        /// </summary>
        protected async Task<string?> GetAttributeWithFallbackAsync(
            string elementName,
            string attributeName,
            params string[] selectors)
        {
            var locator = await Page.FindWithFallbackAsync(elementName, selectors);
            return await locator.GetAttributeAsync(attributeName);
        }

        // ── Self-Healing Visibility/State helpers ─────────────────────

        /// <summary>
        /// Check if element is visible with self-healing fallback selectors.
        /// </summary>
        protected async Task<bool> IsVisibleWithFallbackAsync(
            string elementName,
            params string[] selectors)
        {
            try
            {
                var locator = await Page.FindWithFallbackAsync(elementName, selectors);
                return await locator.IsVisibleAsync();
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check if element is enabled with self-healing fallback selectors.
        /// </summary>
        protected async Task<bool> IsEnabledWithFallbackAsync(
            string elementName,
            params string[] selectors)
        {
            try
            {
                var locator = await Page.FindWithFallbackAsync(elementName, selectors);
                return await locator.IsEnabledAsync();
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check if checkbox is checked with self-healing fallback selectors.
        /// </summary>
        protected async Task<bool> IsCheckedWithFallbackAsync(
            string elementName,
            params string[] selectors)
        {
            try
            {
                var locator = await Page.FindWithFallbackAsync(elementName, selectors);
                return await locator.IsCheckedAsync();
            }
            catch
            {
                return false;
            }
        }

        // ── Self-Healing Wait helpers ─────────────────────────────────

        /// <summary>
        /// Wait for element to be visible with self-healing fallback selectors.
        /// </summary>
        protected async Task WaitForVisibleWithFallbackAsync(
            string elementName,
            int timeoutMs = 15000,
            params string[] selectors)
        {
            var locator = await Page.FindWithFallbackAsync(elementName, selectors);
            await locator.WaitForAsync(new()
            {
                State = WaitForSelectorState.Visible,
                Timeout = timeoutMs
            });
        }

        /// <summary>
        /// Wait for element to be hidden with self-healing fallback selectors.
        /// </summary>
        protected async Task WaitForHiddenWithFallbackAsync(
            string elementName,
            int timeoutMs = 15000,
            params string[] selectors)
        {
            var locator = await Page.FindWithFallbackAsync(elementName, selectors);
            await locator.WaitForAsync(new()
            {
                State = WaitForSelectorState.Hidden,
                Timeout = timeoutMs
            });
        }

        // ── Self-Healing Keyboard helpers ─────────────────────────────

        /// <summary>
        /// Press key on element with self-healing fallback selectors.
        /// </summary>
        protected async Task PressKeyWithFallbackAsync(
            string elementName,
            string key,
            params string[] selectors)
        {
            var locator = await Page.FindWithFallbackAsync(elementName, selectors);
            await locator.PressAsync(key);
        }

        /// <summary>
        /// Type text slowly with self-healing fallback selectors.
        /// </summary>
        protected async Task TypeWithFallbackAsync(
            string elementName,
            string text,
            int delayMs = 50,
            params string[] selectors)
        {
            var locator = await Page.FindWithFallbackAsync(elementName, selectors);
            await locator.TypeAsync(text, new() { Delay = delayMs });
        }

        // ── Self-Healing Drag & Drop helpers ──────────────────────────

        /// <summary>
        /// Drag element to target with self-healing fallback selectors.
        /// </summary>
        protected async Task DragToWithFallbackAsync(
            string sourceElementName,
            string targetElementName,
            string[] sourceSelectors,
            params string[] targetSelectors)
        {
            var sourceLocator = await Page.FindWithFallbackAsync(sourceElementName, sourceSelectors);
            var targetLocator = await Page.FindWithFallbackAsync(targetElementName, targetSelectors);
            await sourceLocator.DragToAsync(targetLocator);
        }

        // ── Self-Healing Screenshot helpers ───────────────────────────

        /// <summary>
        /// Take screenshot of element with self-healing fallback selectors.
        /// </summary>
        protected async Task<byte[]> ScreenshotElementWithFallbackAsync(
            string elementName,
            params string[] selectors)
        {
            var locator = await Page.FindWithFallbackAsync(elementName, selectors);
            return await locator.ScreenshotAsync();
        }

        // ── Assertions ────────────────────────────────────────────────

        protected async Task AssertVisibleAsync(string selector)
        {
            await Assertions.Expect(Page.Locator(selector))
                .ToBeVisibleAsync(new() { Timeout = 10000 });
        }

        protected async Task AssertTextAsync(string selector, string expected)
        {
            await Assertions.Expect(Page.Locator(selector))
                .ToContainTextAsync(expected);
        }

        protected async Task AssertUrlContainsAsync(string expected)
        {
            await Assertions.Expect(Page)
                .ToHaveURLAsync(new Regex(expected));
        }

        protected async Task AssertInputValueAsync(string selector, string expected)
        {
            var actual = await Page.Locator(selector).InputValueAsync();
            if (!string.Equals(actual, expected))
            {
                throw new InvalidOperationException(
                    $"Expected '{selector}' to have value '{expected}', but found '{actual}'.");
            }
        }

        // ── Wait helpers ──────────────────────────────────────────────

        protected async Task WaitForNavigationAsync()
        {
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        }

        protected async Task WaitForSelectorAsync(string selector)
        {
            await Page.Locator(selector).WaitForAsync(
                new()
                {
                    State = WaitForSelectorState.Visible,
                    Timeout = 15000
                });
        }

        // ── Page info ─────────────────────────────────────────────────

        protected async Task<string> GetTitleAsync() =>
            await Page.TitleAsync();

        protected string GetUrl() =>
            Page.Url;

        protected async Task<string> GetTextAsync(string selector) =>
            await Page.Locator(selector).InnerTextAsync();

        // ── Screenshot ────────────────────────────────────────────────

        protected async Task<string> TakeScreenshotAsync(string name = "")
        {
            Directory.CreateDirectory("playwright-screenshots");

            var fileName = string.IsNullOrEmpty(name)
                ? DateTime.Now.Ticks.ToString()
                : name;

            var path = Path.Combine(
                "playwright-screenshots",
                $"{fileName}.png");

            await Page.ScreenshotAsync(new() { Path = path });
            return path;
        }
    }
}