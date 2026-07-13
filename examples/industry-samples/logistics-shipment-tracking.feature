# Illustrative sample only — not part of the executable TalosAI test suite (no bound step definitions).
Feature: Shipment Tracking
  As a logistics operations user
  I want to create shipments and track their delivery progress
  So that customers and dispatchers have visibility into transit status

  Background:
    Given the application is running
    And I navigate to the shipment dashboard

  @logistics @smoke-ui
  Scenario: Create a new shipment with valid origin and destination
    When I create a shipment from "Chicago, IL" to "Austin, TX" with weight "12.5" kg
    Then I should see a confirmation containing "Shipment created"
    And the shipment should have a tracking number

  @logistics @negative @smoke-ui
  Scenario: Shipment creation is rejected when destination address is missing
    When I create a shipment from "Chicago, IL" to "" with weight "12.5" kg
    Then I should see an error message containing "Destination address is required"

  @logistics @api @APISanity
  Scenario: Fetch shipment status via the tracking API
    # Endpoint is illustrative only: https://api.example-logistics.com/v1/shipments/{trackingNumber}
    Given I have a valid API authorization token
    When I send a GET request to "/v1/shipments/SHP-88213401"
    Then the API response status code should be 200
    And the response should contain field "status"
    And the response should contain field "estimatedDelivery"

  @logistics @api @APISanity
  Scenario Outline: Shipment status codes map to expected customer-facing labels
    # Endpoint is illustrative only: https://api.example-logistics.com/v1/shipments/status
    Given I have a valid API authorization token
    When I send a GET request to "/v1/shipments/status" with query parameters:
      | statusCode | <StatusCode> |
    Then the API response status code should be 200
    And the response should contain field "label"

    Examples:
      | StatusCode |
      | IN_TRANSIT |
      | OUT_FOR_DELIVERY |
      | DELIVERED  |
      | EXCEPTION  |

  @logistics @smoke-ui
  Scenario: Delivery exception raises an alert on the dashboard
    When a shipment with tracking number "SHP-88213401" reports a delivery exception
    Then I should see an alert banner containing "Delivery exception"
    And the shipment status should be labeled "Action Required"
