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
    public class ProductCatalogSteps
    {
        private readonly IWebDriver? _driver;
        private readonly WebDriverWait? _wait;

        // saucedemo.com's add-to-cart button ids don't follow a fully regular
        // slug pattern (one product name includes punctuation), so known
        // products are mapped explicitly rather than guessed.
        private static readonly Dictionary<string, string> ProductSlugs = new()
        {
            ["Sauce Labs Backpack"] = "sauce-labs-backpack",
            ["Sauce Labs Bike Light"] = "sauce-labs-bike-light",
            ["Sauce Labs Bolt T-Shirt"] = "sauce-labs-bolt-t-shirt",
            ["Sauce Labs Fleece Jacket"] = "sauce-labs-fleece-jacket",
            ["Sauce Labs Onesie"] = "sauce-labs-onesie",
            ["Test.allTheThings() T-Shirt (Red)"] = "test.allthethings()-t-shirt-(red)"
        };

        public ProductCatalogSteps(IObjectContainer container)
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

        private static string SlugFor(string productName)
        {
            if (!ProductSlugs.TryGetValue(productName, out var slug))
                throw new ArgumentException($"Unknown demo product '{productName}'.");
            return slug;
        }

        [When(@"I add ""(.*)"" to the cart")]
        public void WhenIAddToTheCart(string productName)
        {
            _wait!.Until(ExpectedConditions.ElementToBeClickable(
                ProductCatalogPage.AddToCartButton(SlugFor(productName)))).Click();
        }

        [When(@"I remove ""(.*)"" from the cart")]
        public void WhenIRemoveFromTheCart(string productName)
        {
            _wait!.Until(ExpectedConditions.ElementToBeClickable(
                ProductCatalogPage.RemoveButton(SlugFor(productName)))).Click();
        }

        [Then(@"the cart badge should show (\d+)")]
        public void ThenTheCartBadgeShouldShow(int expectedCount)
        {
            var badge = _wait!.Until(ExpectedConditions.ElementIsVisible(ProductCatalogPage.CartBadge));
            badge.Text.Should().Be(expectedCount.ToString());
        }

        [Then("the cart badge should not be visible")]
        public void ThenTheCartBadgeShouldNotBeVisible()
        {
            _driver!.FindElements(ProductCatalogPage.CartBadge).Should().BeEmpty();
        }

        [When("I open the cart")]
        public void WhenIOpenTheCart()
        {
            _wait!.Until(ExpectedConditions.ElementToBeClickable(ProductCatalogPage.CartLink)).Click();
        }

        [Then(@"the cart should contain ""(.*)""")]
        public void ThenTheCartShouldContain(string productName)
        {
            _wait!.Until(ExpectedConditions.ElementIsVisible(ProductCatalogPage.CartItemName(productName)));
        }

        [Then(@"the cart should contain (\d+) items")]
        public void ThenTheCartShouldContainItems(int expectedCount)
        {
            var items = _driver!.FindElements(ProductCatalogPage.CartItems);
            items.Count.Should().Be(expectedCount);
        }
    }
}
