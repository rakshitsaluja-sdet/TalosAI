# Illustrative sample only — not part of the executable TalosAI test suite (no bound step definitions).
Feature: Loan Processing
  As a retail banking customer
  I want to apply for a personal loan and track its approval status
  So that I can borrow funds without visiting a branch

  Background:
    Given the application is running
    And I navigate to the loan application page

  @bfsi @smoke-ui
  Scenario: Submit a personal loan application with valid details
    When I fill in the loan application with amount "15000" tenure "36" months and monthly income "6000"
    And I submit the loan application
    Then I should see a confirmation containing "Application submitted"

  @bfsi @negative @smoke-ui
  Scenario: Loan application is rejected when requested amount exceeds eligibility
    When I fill in the loan application with amount "500000" tenure "12" months and monthly income "2000"
    And I submit the loan application
    Then I should see an error message containing "exceeds eligible limit"

  @bfsi @api @APISanity
  Scenario: Fetch loan eligibility via the eligibility API
    # Endpoint is illustrative only: https://api.example-bank.com/v1/loans/eligibility
    Given I have a valid API authorization token
    When I send a GET request to "/v1/loans/eligibility" with query parameters:
      | customerId    | CUST-10234 |
      | monthlyIncome | 6000       |
    Then the API response status code should be 200
    And the response should contain field "eligibleAmount"
    And the response should contain field "interestRate"

  @bfsi @api @APISanity
  Scenario Outline: Interest rate varies by credit tier
    # Endpoint is illustrative only: https://api.example-bank.com/v1/loans/rate-card
    Given I have a valid API authorization token
    When I send a GET request to "/v1/loans/rate-card" with query parameters:
      | creditTier | <CreditTier> |
    Then the API response status code should be 200
    And the response should contain field "annualInterestRate"

    Examples:
      | CreditTier |
      | AAA        |
      | AA         |
      | B          |

  @bfsi @smoke-ui
  Scenario: Loan status page reflects the latest application decision
    When I search for loan application reference "LN-2026-00981"
    Then I should see the loan status labeled "Under Review"
