using OpenQA.Selenium;

namespace TalosAI.Automation.Pages
{
    /// <summary>Locators for the saucedemo.com login page.</summary>
    public static class LoginPage
    {
        public static By UsernameInput => By.Id("user-name");
        public static By PasswordInput => By.Id("password");
        public static By LoginButton => By.Id("login-button");
        public static By ErrorMessage => By.CssSelector("[data-test='error']");
    }
}
