Feature: Inventory
  As a signed-in shopper
  I want to view and sort the product inventory
  So that I can find items in the order I prefer

  Background:
    Given the application is running
    And I navigate to the login page
    When I log in with username "standard_user" and password "secret_sauce"

  @inventory @smoke-ui
  Scenario: Inventory page loads with all products
    Then I should see the products page title
    And the inventory should list 6 items

  @inventory @sort @smoke-ui
  Scenario: Sort products by name A to Z
    When I sort products by "Name (A to Z)"
    Then the product names should be sorted alphabetically ascending

  @inventory @sort @smoke-ui
  Scenario: Sort products by name Z to A
    When I sort products by "Name (Z to A)"
    Then the product names should be sorted alphabetically descending

  @inventory @sort @smoke-ui
  Scenario: Sort products by price low to high
    When I sort products by "Price (low to high)"
    Then the product prices should be sorted from low to high

  @inventory @sort @smoke-ui
  Scenario: Sort products by price high to low
    When I sort products by "Price (high to low)"
    Then the product prices should be sorted from high to low
