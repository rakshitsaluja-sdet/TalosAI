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
    public class CheckoutSteps
    {
        private readonly IWebDriver? _driver;
        private readonly WebDriverWait? _wait;

        public CheckoutSteps(IObjectContainer container)
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

        [When("I proceed to checkout")]
        public void WhenIProceedToCheckout()
        {
            _wait!.Until(ExpectedConditions.ElementToBeClickable(ProductCatalogPage.CheckoutButton)).Click();
        }

        [When(@"I enter checkout information first name ""(.*)"" last name ""(.*)"" postal code ""(.*)""")]
        public void WhenIEnterCheckoutInformation(string firstName, string lastName, string postalCode)
        {
            if (!string.IsNullOrEmpty(firstName))
                _driver!.FindElement(CheckoutPage.FirstNameInput).SendKeys(firstName);
            if (!string.IsNullOrEmpty(lastName))
                _driver!.FindElement(CheckoutPage.LastNameInput).SendKeys(lastName);
            if (!string.IsNullOrEmpty(postalCode))
                _driver!.FindElement(CheckoutPage.PostalCodeInput).SendKeys(postalCode);
        }

        [When("I continue checkout")]
        public void WhenIContinueCheckout()
        {
            _wait!.Until(ExpectedConditions.ElementToBeClickable(CheckoutPage.ContinueButton)).Click();
        }

        [When("I cancel checkout")]
        public void WhenICancelCheckout()
        {
            _wait!.Until(ExpectedConditions.ElementToBeClickable(CheckoutPage.CancelButton)).Click();
        }

        [When("I finish checkout")]
        public void WhenIFinishCheckout()
        {
            _wait!.Until(ExpectedConditions.ElementToBeClickable(CheckoutPage.FinishButton)).Click();
        }

        [Then(@"I should see a checkout error containing ""(.*)""")]
        public void ThenIShouldSeeACheckoutErrorContaining(string expectedText)
        {
            var error = _wait!.Until(ExpectedConditions.ElementIsVisible(CheckoutPage.ErrorMessage));
            error.Text.Should().ContainEquivalentOf(expectedText);
        }

        [Then("the order summary total should equal the subtotal plus tax")]
        public void ThenTheOrderSummaryTotalShouldEqualSubtotalPlusTax()
        {
            decimal ParseAmount(string text) => decimal.Parse(text.Split('$').Last());

            var subtotal = ParseAmount(_wait!.Until(ExpectedConditions.ElementIsVisible(CheckoutPage.SummarySubtotal)).Text);
            var tax = ParseAmount(_driver!.FindElement(CheckoutPage.SummaryTax).Text);
            var total = ParseAmount(_driver.FindElement(CheckoutPage.SummaryTotal).Text);

            total.Should().Be(subtotal + tax);
        }

        [Then("I should see the order complete confirmation")]
        public void ThenIShouldSeeTheOrderCompleteConfirmation()
        {
            var header = _wait!.Until(ExpectedConditions.ElementIsVisible(CheckoutPage.CompleteHeader));
            header.Text.Should().ContainEquivalentOf("Thank you");
        }
    }
}
