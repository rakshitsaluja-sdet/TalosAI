# Illustrative sample only — not part of the executable TalosAI test suite (no bound step definitions).
Feature: Insurance Claims
  As a policyholder
  I want to submit a claim and check its processing status
  So that I can be reimbursed for a covered loss

  Background:
    Given the application is running
    And I navigate to the claims portal

  @insurance @api @APISanity
  Scenario: Submit a new claim via the claims API
    # Endpoint is illustrative only: https://api.example-insurer.com/v1/claims
    Given I have a valid API authorization token
    When I send a POST request to "/v1/claims" with body:
      """
      {
        "policyNumber": "POL-4471-2026",
        "incidentDate": "2026-07-01",
        "claimType": "Auto Collision",
        "estimatedLossAmount": 3200.00
      }
      """
    Then the API response status code should be 201
    And the response should contain field "claimId"
    And the response should contain field "status"

  @insurance @api @negative @APISanity
  Scenario: Claim submission is rejected when policy number is missing
    # Endpoint is illustrative only: https://api.example-insurer.com/v1/claims
    Given I have a valid API authorization token
    When I send a POST request to "/v1/claims" with body:
      """
      {
        "incidentDate": "2026-07-01",
        "claimType": "Auto Collision",
        "estimatedLossAmount": 3200.00
      }
      """
    Then the API response status code should be 400
    And the response should contain field "error"

  @insurance @api @APISanity
  Scenario Outline: Claim status transitions are reported correctly
    # Endpoint is illustrative only: https://api.example-insurer.com/v1/claims/{claimId}
    Given I have a valid API authorization token
    When I send a GET request to "/v1/claims/CLM-990211"
    Then the API response status code should be 200
    And the response should contain field "<Field>"

    Examples:
      | Field           |
      | status          |
      | adjusterName    |
      | lastUpdatedDate |

  @insurance @smoke-ui
  Scenario: Claim status is visible to the policyholder in the portal
    When I search for claim reference "CLM-990211"
    Then I should see the claim status labeled "In Review"
    And I should see the assigned adjuster name

  @insurance @smoke-ui
  Scenario: Policyholder can upload supporting documents to an open claim
    When I search for claim reference "CLM-990211"
    And I upload a supporting document named "collision-photo.jpg"
    Then I should see a confirmation containing "Document uploaded"
