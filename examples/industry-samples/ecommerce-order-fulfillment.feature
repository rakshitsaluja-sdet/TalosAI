# Illustrative sample only — not part of the executable TalosAI test suite (no bound step definitions).
Feature: Order Fulfillment
  As a fulfillment center operator
  I want to process and update orders from receipt through shipment
  So that customer orders are picked, packed, and shipped accurately

  Background:
    Given the application is running
    And I navigate to the fulfillment dashboard

  @ecommerce @smoke-ui
  Scenario: Pick and pack a newly placed order
    When I open order "ORD-55231" from the fulfillment queue
    And I mark all line items as picked
    And I mark the order as packed
    Then I should see the order status labeled "Ready to Ship"

  @ecommerce @negative @smoke-ui
  Scenario: Order cannot be marked as packed with unpicked line items
    When I open order "ORD-55231" from the fulfillment queue
    And I mark the order as packed
    Then I should see an error message containing "All items must be picked"

  @ecommerce @api @APISanity
  Scenario: Fetch order details via the orders API
    # Endpoint is illustrative only: https://api.example-shop.com/v1/orders/{orderId}
    Given I have a valid API authorization token
    When I send a GET request to "/v1/orders/ORD-55231"
    Then the API response status code should be 200
    And the response should contain field "items"
    And the response should contain field "shippingAddress"

  @ecommerce @api @APISanity
  Scenario Outline: Inventory levels decrement correctly after order confirmation
    # Endpoint is illustrative only: https://api.example-shop.com/v1/inventory/{sku}
    Given I have a valid API authorization token
    When I send a GET request to "/v1/inventory/<Sku>"
    Then the API response status code should be 200
    And the response should contain field "availableQuantity"

    Examples:
      | Sku          |
      | SKU-BACKPACK |
      | SKU-BIKELOCK |
      | SKU-TSHIRT   |

  @ecommerce @smoke-ui
  Scenario: Shipping an order generates a tracking number
    When I open order "ORD-55231" from the fulfillment queue
    And I mark the order as shipped via carrier "Ground Freight"
    Then I should see a confirmation containing "Shipping label created"
    And the order should have a tracking number
