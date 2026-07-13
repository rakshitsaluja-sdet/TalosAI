Feature: Checkout
  As a signed-in shopper with items in my cart
  I want to complete or cancel the checkout flow
  So that required-field validation and order totals behave correctly

  Background:
    Given the application is running
    And I navigate to the login page
    When I log in with username "standard_user" and password "secret_sauce"
    And I add "Sauce Labs Backpack" to the cart
    And I open the cart
    And I proceed to checkout

  @checkout @validation @smoke-ui
  Scenario: Missing first name is rejected
    When I enter checkout information first name "" last name "Doe" postal code "12345"
    And I continue checkout
    Then I should see a checkout error containing "First Name is required"

  @checkout @validation @smoke-ui
  Scenario: Missing last name is rejected
    When I enter checkout information first name "Jane" last name "" postal code "12345"
    And I continue checkout
    Then I should see a checkout error containing "Last Name is required"

  @checkout @validation @smoke-ui
  Scenario: Missing postal code is rejected
    When I enter checkout information first name "Jane" last name "Doe" postal code ""
    And I continue checkout
    Then I should see a checkout error containing "Postal Code is required"

  @checkout @smoke-ui
  Scenario: Cancel returns to the cart without completing the order
    When I cancel checkout
    Then the cart badge should show 1

  @checkout @smoke-ui
  Scenario: Completing checkout shows a consistent total and confirmation
    When I enter checkout information first name "Jane" last name "Doe" postal code "12345"
    And I continue checkout
    Then the order summary total should equal the subtotal plus tax
    When I finish checkout
    Then I should see the order complete confirmation
