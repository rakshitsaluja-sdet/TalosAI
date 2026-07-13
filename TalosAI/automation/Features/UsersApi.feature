Feature: Users API
  As an API consumer
  I want to query the public reqres.in Users API
  So that I can validate pagination and response shape

  @api @users-api @APISanity
  Scenario: Get a page of users
    Given I have a valid API authorization token
    When I send a GET request to "/api/users" with query parameters:
      | page     | 2 |
      | per_page | 3 |
    Then the API response status code should be 200
    And the response should contain field "page"
    And the response should contain field "total"
    And the response should contain field "data"

  @api @users-api @APISanity
  Scenario Outline: Get different pages of users
    Given I have a valid API authorization token
    When I send a GET request to "/api/users" with query parameters:
      | page     | <Page>    |
      | per_page | <PerPage> |
    Then the API response status code should be 200
    And the response should contain the following fields:
      | page        |
      | per_page    |
      | total       |
      | total_pages |
    And the response time should be less than 5000 milliseconds

    Examples:
      | Page | PerPage |
      | 1    | 3       |
      | 2    | 3       |
      | 1    | 6       |

  @api @users-api @APISanity
  Scenario: Get a single user by ID
    Given I have a valid API authorization token
    When I send a GET request to "/api/users/2"
    Then the API response status code should be 200
    And the response should contain field "data"
