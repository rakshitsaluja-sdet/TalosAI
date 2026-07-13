using OpenQA.Selenium;

namespace TalosAI.Automation.Pages
{
    /// <summary>Locators for adding products to the cart and reviewing cart contents on saucedemo.com.</summary>
    public static class ProductCatalogPage
    {
        public static By AddToCartButton(string productSlug) => By.Id($"add-to-cart-{productSlug}");
        public static By RemoveButton(string productSlug) => By.Id($"remove-{productSlug}");
        public static By CartBadge => By.CssSelector(".shopping_cart_badge");
        public static By CartLink => By.CssSelector(".shopping_cart_link");
        public static By CartItems => By.CssSelector(".cart_item");
        public static By CartItemName(string name) =>
            By.XPath($"//div[@class='cart_item']//div[@class='inventory_item_name' and normalize-space(.)='{name}']");
        public static By CheckoutButton => By.Id("checkout");
    }
}
