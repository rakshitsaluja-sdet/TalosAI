using TalosAI.Automation.Pages;
using FluentAssertions;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using TechTalk.SpecFlow;
using BoDi;

namespace TalosAI.Automation.Steps
{
    [Binding]
    public class InventorySteps
    {
        private readonly IWebDriver? _driver;
        private readonly WebDriverWait? _wait;

        public InventorySteps(IObjectContainer container)
        {
            try
            {
                _driver = container.Resolve<IWebDriver>();
                _wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(30));
            }
            catch
            {
                _driver = null;
                _wait = null;
            }
        }

        [Then("I should see the products page title")]
        public void ThenIShouldSeeTheProductsPageTitle()
        {
            var title = _wait!.Until(ExpectedConditions.ElementIsVisible(InventoryPage.PageTitle));
            title.Text.Should().Be("Products");
        }

        [Then(@"the inventory should list (\d+) items")]
        public void ThenTheInventoryShouldListItems(int expectedCount)
        {
            _wait!.Until(ExpectedConditions.ElementIsVisible(InventoryPage.InventoryItems));
            var items = _driver!.FindElements(InventoryPage.InventoryItems);
            items.Count.Should().Be(expectedCount);
        }

        [When(@"I sort products by ""(.*)""")]
        public void WhenISortProductsBy(string sortOptionLabel)
        {
            var dropdown = _wait!.Until(ExpectedConditions.ElementIsVisible(InventoryPage.SortDropdown));
            new SelectElement(dropdown).SelectByText(sortOptionLabel);
        }

        [Then("the product names should be sorted alphabetically ascending")]
        public void ThenTheProductNamesShouldBeSortedAlphabeticallyAscending()
        {
            var names = _driver!.FindElements(InventoryPage.ItemNames).Select(e => e.Text).ToList();
            names.Should().BeInAscendingOrder();
        }

        [Then("the product names should be sorted alphabetically descending")]
        public void ThenTheProductNamesShouldBeSortedAlphabeticallyDescending()
        {
            var names = _driver!.FindElements(InventoryPage.ItemNames).Select(e => e.Text).ToList();
            names.Should().BeInDescendingOrder();
        }

        [Then("the product prices should be sorted from low to high")]
        public void ThenTheProductPricesShouldBeSortedFromLowToHigh()
        {
            var prices = ReadPrices();
            prices.Should().BeInAscendingOrder();
        }

        [Then("the product prices should be sorted from high to low")]
        public void ThenTheProductPricesShouldBeSortedFromHighToLow()
        {
            var prices = ReadPrices();
            prices.Should().BeInDescendingOrder();
        }

        private List<decimal> ReadPrices()
        {
            return _driver!.FindElements(InventoryPage.ItemPrices)
                .Select(e => decimal.Parse(e.Text.TrimStart('$')))
                .ToList();
        }
    }
}
