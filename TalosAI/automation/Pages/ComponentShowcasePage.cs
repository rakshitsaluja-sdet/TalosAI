using TalosAI.Core.Runner;
using Microsoft.Playwright;

namespace TalosAI.Automation.Pages
{
    /// <summary>
    /// Playwright page object for a handful of the-internet.herokuapp.com's
    /// intentionally tricky UI patterns (dropdown, JS-delayed content, broken
    /// images) — a good showcase for explicit waits and self-healing selectors.
    /// </summary>
    public class ComponentShowcasePage : PlaywrightBasePage
    {
        private const string DropdownSelector = "#dropdown";
        private const string StartButtonSelector = "#start button";
        private const string LoadedTextSelector = "#finish h4";
        private const string ImageSelector = "img";

        public ComponentShowcasePage(PlaywrightDriver driver) : base(driver) { }

        public Task NavigateToDropdownAsync() =>
            NavigateToAsync("https://the-internet.herokuapp.com/dropdown");

        public Task NavigateToDynamicLoadingAsync(int example) =>
            NavigateToAsync($"https://the-internet.herokuapp.com/dynamic_loading/{example}");

        public Task NavigateToBrokenImagesAsync() =>
            NavigateToAsync("https://the-internet.herokuapp.com/broken_images");

        public async Task SelectDropdownOptionAsync(string optionText)
        {
            await Page.Locator(DropdownSelector).SelectOptionAsync(new SelectOptionValue { Label = optionText });
        }

        public async Task<string> GetSelectedDropdownOptionAsync()
        {
            return await Page.Locator($"{DropdownSelector} option:checked").InnerTextAsync();
        }

        public async Task ClickStartAndWaitForContentAsync()
        {
            await Page.Locator(StartButtonSelector).ClickAsync();
            await Page.Locator(LoadedTextSelector).WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        }

        public Task<string> GetLoadedTextAsync() => Page.Locator(LoadedTextSelector).InnerTextAsync();

        /// <summary>
        /// Counts images whose natural width is 0 — the browser's own signal
        /// that an &lt;img&gt; failed to load, used instead of a fixed locator
        /// since broken images don't carry a distinguishing class/attribute.
        /// </summary>
        public async Task<int> CountBrokenImagesAsync()
        {
            var images = Page.Locator(ImageSelector);
            var count = await images.CountAsync();
            var brokenCount = 0;

            for (var i = 0; i < count; i++)
            {
                var naturalWidth = await images.Nth(i).EvaluateAsync<int>("img => img.naturalWidth");
                if (naturalWidth == 0)
                    brokenCount++;
            }

            return brokenCount;
        }
    }
}
