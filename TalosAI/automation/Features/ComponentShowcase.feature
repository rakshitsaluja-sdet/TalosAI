@playwright
Feature: Component showcase
  As a QA engineer evaluating this framework
  I want example scenarios against intentionally tricky UI patterns
  So that I can see explicit waits and self-healing selectors in action

  @showcase @dropdown @smoke-ui
  Scenario: Selecting a dropdown option
    Given I navigate to the dropdown showcase page
    When I select "Option 2" from the dropdown
    Then the dropdown should show "Option 2" selected

  @showcase @dynamic-loading @smoke-ui
  Scenario: Waiting for JavaScript-delayed content to appear
    Given I navigate to dynamic loading example 1
    When I click start and wait for the content to load
    Then I should see the text "Hello World!"

  @showcase @broken-images @smoke-ui
  Scenario: Detecting broken images on a page
    Given I navigate to the broken images showcase page
    Then at least 1 broken image should be detected
