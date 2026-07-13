# Illustrative sample only — not part of the executable TalosAI test suite (no bound step definitions).
Feature: Ride Booking
  As a rider using the mobility app
  I want to request a ride and see an accurate fare estimate
  So that I can get from my pickup point to my destination reliably

  Background:
    Given the application is running
    And I navigate to the ride booking page

  @mobility @smoke-ui
  Scenario: Book a ride with valid pickup and drop-off locations
    When I request a ride from "5th Ave & 42nd St" to "JFK Airport"
    And I confirm the ride request
    Then I should see a confirmation containing "Driver assigned"
    And I should see an estimated arrival time

  @mobility @negative @smoke-ui
  Scenario: Ride request is rejected when drop-off location is missing
    When I request a ride from "5th Ave & 42nd St" to ""
    Then I should see an error message containing "Destination is required"

  @mobility @api @APISanity
  Scenario: Fetch a fare estimate via the pricing API
    # Endpoint is illustrative only: https://api.example-rides.com/v1/fare-estimate
    Given I have a valid API authorization token
    When I send a GET request to "/v1/fare-estimate" with query parameters:
      | pickupLat   | 40.7580 |
      | pickupLng   | -73.9855 |
      | dropoffLat  | 40.6413 |
      | dropoffLng  | -73.7781 |
    Then the API response status code should be 200
    And the response should contain field "estimatedFare"
    And the response should contain field "currency"

  @mobility @api @APISanity
  Scenario Outline: Fare estimate varies by ride tier
    # Endpoint is illustrative only: https://api.example-rides.com/v1/fare-estimate
    Given I have a valid API authorization token
    When I send a GET request to "/v1/fare-estimate" with query parameters:
      | rideTier | <RideTier> |
    Then the API response status code should be 200
    And the response should contain field "estimatedFare"

    Examples:
      | RideTier |
      | Economy  |
      | Comfort  |
      | XL       |

  @mobility @smoke-ui
  Scenario: Rider can cancel a ride before driver arrival
    When I request a ride from "5th Ave & 42nd St" to "JFK Airport"
    And I confirm the ride request
    And I cancel the ride
    Then I should see a confirmation containing "Ride cancelled"
