using OpenQA.Selenium;

namespace TalosAI.Automation.Pages
{
    /// <summary>Locators for the saucedemo.com checkout flow (info form, overview, complete).</summary>
    public static class CheckoutPage
    {
        public static By FirstNameInput => By.Id("first-name");
        public static By LastNameInput => By.Id("last-name");
        public static By PostalCodeInput => By.Id("postal-code");
        public static By ContinueButton => By.Id("continue");
        public static By CancelButton => By.Id("cancel");
        public static By ErrorMessage => By.CssSelector("[data-test='error']");
        public static By SummarySubtotal => By.CssSelector(".summary_subtotal_label");
        public static By SummaryTax => By.CssSelector(".summary_tax_label");
        public static By SummaryTotal => By.CssSelector(".summary_total_label");
        public static By FinishButton => By.Id("finish");
        public static By CompleteHeader => By.CssSelector(".complete-header");
    }
}
