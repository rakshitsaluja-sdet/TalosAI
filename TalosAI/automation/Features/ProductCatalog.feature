Feature: Shopping cart
  As a signed-in shopper
  I want to add and remove products from my cart
  So that I can verify the cart accurately reflects my selections

  Background:
    Given the application is running
    And I navigate to the login page
    When I log in with username "standard_user" and password "secret_sauce"

  @cart @smoke-ui
  Scenario: Adding a product updates the cart badge
    When I add "Sauce Labs Backpack" to the cart
    Then the cart badge should show 1

  @cart @smoke-ui
  Scenario: Adding multiple products updates the cart badge accordingly
    When I add "Sauce Labs Backpack" to the cart
    And I add "Sauce Labs Bike Light" to the cart
    And I add "Sauce Labs Onesie" to the cart
    Then the cart badge should show 3

  @cart @smoke-ui
  Scenario: Removing a product from the inventory page updates the cart badge
    When I add "Sauce Labs Backpack" to the cart
    And I add "Sauce Labs Bike Light" to the cart
    And I remove "Sauce Labs Backpack" from the cart
    Then the cart badge should show 1

  @cart @smoke-ui
  Scenario: Removing the only product clears the cart badge
    When I add "Sauce Labs Backpack" to the cart
    And I remove "Sauce Labs Backpack" from the cart
    Then the cart badge should not be visible

  @cart @smoke-ui
  Scenario: Cart page lists exactly the products that were added
    When I add "Sauce Labs Backpack" to the cart
    And I add "Sauce Labs Fleece Jacket" to the cart
    And I open the cart
    Then the cart should contain "Sauce Labs Backpack"
    And the cart should contain "Sauce Labs Fleece Jacket"
    And the cart should contain 2 items
