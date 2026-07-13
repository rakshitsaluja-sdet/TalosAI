Feature: Update user using data-driven examples

@POCOLearning @api
 Scenario Outline: Update an existing user with different values
  Given user has update user payload with name "<name>" and job "<job>"
  When user updates the user via PUT API
  Then response status code should be 201
  And response should contain updated user details

Examples:
  | name    | job       |
  | Michael | Manager   |
  | Priya   | Architect |
  | John    | QA Lead   |