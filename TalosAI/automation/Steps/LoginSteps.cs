using TalosAI.Automation.Pages;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using FluentAssertions;
using TechTalk.SpecFlow;
using BoDi;

namespace TalosAI.Automation.Steps
{
    [Binding]
    public class LoginSteps
    {
        private readonly IWebDriver? _driver;
        private readonly WebDriverWait? _wait;

        public LoginSteps(IObjectContainer container)
        {
            // Only resolve driver if it's registered (UI scenarios)
            try
            {
                _driver = container.Resolve<IWebDriver>();
                _wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(30));
            }
            catch
            {
                // Driver not registered - this is an API-only scenario
                _driver = null;
                _wait = null;
            }
        }

        [Given("the application is running")]
        public void GivenTheApplicationIsRunning()
        {
            // No-op — the target site is a public, always-on demo.
        }

        [Given("I navigate to the login page")]
        public void GivenINavigateToTheLoginPage()
        {
            _driver!.Navigate().GoToUrl(TalosAI.core.Utils.BaseTest.BaseUrl);
        }

        [When(@"I log in with username ""(.*)"" and password ""(.*)""")]
        public void WhenILogInWithUsernameAndPassword(string username, string password)
        {
            _wait!.Until(ExpectedConditions.ElementIsVisible(LoginPage.UsernameInput)).SendKeys(username);
            _driver!.FindElement(LoginPage.PasswordInput).SendKeys(password);
            _driver.FindElement(LoginPage.LoginButton).Click();
        }

        [Then("I should be logged in and see the inventory page")]
        public void ThenIShouldBeLoggedInAndSeeTheInventoryPage()
        {
            _wait!.Until(ExpectedConditions.UrlContains("inventory.html"));
        }

        [Then(@"I should see an error message containing ""(.*)""")]
        public void ThenIShouldSeeAnErrorMessageContaining(string expectedText)
        {
            var error = _wait!.Until(ExpectedConditions.ElementIsVisible(LoginPage.ErrorMessage));
            error.Text.Should().ContainEquivalentOf(expectedText);
        }
    }
}
