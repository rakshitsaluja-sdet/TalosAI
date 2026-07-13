using OpenQA.Selenium;

namespace TalosAI.Automation.Pages
{
    /// <summary>Locators for the saucedemo.com inventory (product listing) page.</summary>
    public static class InventoryPage
    {
        public static By PageTitle => By.CssSelector(".title");
        public static By InventoryItems => By.CssSelector(".inventory_item");
        public static By ItemName(string name) =>
            By.XPath($"//div[@class='inventory_item_name' and normalize-space(.)='{name}']");
        public static By ItemPrice(string name) => By.XPath(
            $"//div[@class='inventory_item_name' and normalize-space(.)='{name}']" +
            "/ancestor::div[@class='inventory_item']//div[@class='inventory_item_price']");
        public static By SortDropdown => By.CssSelector("[data-test='product-sort-container']");
        public static By ItemPrices => By.CssSelector(".inventory_item_price");
        public static By ItemNames => By.CssSelector(".inventory_item_name");
        public static By ShoppingCartLink => By.CssSelector(".shopping_cart_link");
        public static By ShoppingCartBadge => By.CssSelector(".shopping_cart_badge");
        public static By AddToCartButton(string productSlug) => By.Id($"add-to-cart-{productSlug}");
    }
}
