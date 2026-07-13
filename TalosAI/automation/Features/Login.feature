Feature: Login
  As a user of the demo store
  I want to sign in with valid credentials
  So that I can access the product inventory

  Background:
    Given the application is running
    And I navigate to the login page

  @login @smoke-ui
  Scenario: Sign in with a valid standard user
    When I log in with username "standard_user" and password "secret_sauce"
    Then I should be logged in and see the inventory page

  @login @negative @smoke-ui
  Scenario: Sign in with an invalid password is rejected
    When I log in with username "standard_user" and password "wrong_password"
    Then I should see an error message containing "do not match"

  @login @negative @smoke-ui
  Scenario: Sign in as a locked-out user is rejected
    When I log in with username "locked_out_user" and password "secret_sauce"
    Then I should see an error message containing "locked out"
