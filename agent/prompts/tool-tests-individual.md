# Individual Tool Test Prompts

Natural-language prompts for exercising each MCPBridge tool category on its own.
Paste these into the Agent UI (`http://localhost:8080`) or GitHub Copilot Chat
(Agent mode, MCP tools connected — see `docs/SHOWCASE-GUIDE.md`). Run them in
order within a section; each builds on the previous tool call's state
(browser session, configured API/DB, etc).

MCPBridge must be running (`http://localhost:5555/health` returns `ok`) before
any of these will work.

## 1. Playwright — primary UI engine

```
Launch a Chromium browser (not headless) and navigate to https://www.saucedemo.com/
Fill the username field (#user-name) with "standard_user" and the password field (#password) with "secret_sauce"
Click the login button (#login-button)
Assert the page title element (.title) is visible
Take a screenshot named "inventory-page"
Assert the URL contains "inventory.html"
Get the text of the first product name (.inventory_item_name)
Check whether the shopping cart badge (.shopping_cart_badge) is visible
Hover over the sort dropdown ([data-test='product-sort-container'])
Select the option "Price (low to high)" from that dropdown
Wait for the inventory list (.inventory_list) to be visible
Close the browser
```

## 2. Selenium — fallback engine (direct)

```
Launch a browser, then navigate (Selenium-style) to https://the-internet.herokuapp.com/dropdown
Find the element with CSS selector #dropdown
Select the dropdown option "Option 2" by value
Get the page title and current URL
Scroll to the element with CSS selector h3
Take a full-page screenshot
Get the "class" attribute of the h3 element
Refresh the page, then go back, then go forward
Close the browser
```

## 3. API / RestSharp

```
Configure the API base URL as https://reqres.in
Send a GET request to /api/users?page=2
Assert the response status code is 200
Send a POST request to /api/users with body {"name": "Ada", "job": "Engineer"}
Assert the response status code is 201
Assert the JSON path "name" equals "Ada"
Send a PUT request to /api/users/2 with body {"name": "Ada Lovelace"}
Send a DELETE request to /api/users/2
Assert the response status code is 204
```

## 4. Database (SQLite/SQL Server via DatabaseToolHandler)

```
Configure the database with connection string "Data Source=./TestData/sample.db" (or your SQL Server string)
Execute the query "SELECT COUNT(*) FROM SyncedUsers" (read-only, no allow_write needed)
Verify data exists in table "SyncedUsers" where Name equals "Ada Lovelace"
Insert test data into "SyncedUsers": {"Id": "999", "Name": "Test User", "CreatedAt": "2026-01-01"}
Get the table schema for "SyncedUsers"
Delete test data from "SyncedUsers" where Id equals "999"
```
Note: `execute_non_query`, `execute_stored_procedure`, and non-SELECT `execute_query` calls
require `allow_write: true` — this is intentional (see Phase 1 security fixes).

## 5. Performance (NBomber)

```
Configure a performance test against https://reqres.in
Run a load test on GET /api/users for 30 seconds with 10 concurrent users
Get the performance summary
Export the performance report to "reports/load-test-1.html"
```

## 6. Test Data (Bogus)

```
Generate 5 fake person records
Generate 3 fake user accounts
Generate a batch of test data: 10 users, 10 products, 10 orders
```

## 7. Image comparison (Magick.NET)

```
Take a screenshot of the current page named "baseline.png"
(make a small change, e.g. resize the window or navigate)
Take another screenshot named "current.png"
Compare "baseline.png" and "current.png" with a 5% difference threshold
Annotate "current.png" with the text "CHANGED" at position (10, 10)
Get the image properties of "current.png"
```

## 8. Reporting (Allure / ExtentReports)

```
Configure reporting for this session
Start a test named "Smoke - Login"
Log a test step: "Navigated to login page"
Log a test step: "Entered valid credentials"
End the test as passed
Generate the report
Get the test statistics
```

## 9. Azure DevOps

```
Configure Azure DevOps for organization "<your-org>" and project "<your-project>" (requires AZURE_DEVOPS_PAT env var or a pat argument)
Get the active user stories
Get user story #1234
Get user stories by iteration "Sprint 12"
Generate test scenarios for user story #1234
```

## 10. SpecFlow (run the compiled suite)

```
List all scenarios in the TalosAI project
Run the feature "Login.feature"
Run the scenario matching name "Sign in with a valid standard user"
Parse the last test results
```

## 11. Solution Reader / Writer

```
Scan the solution structure
Read all feature files in TalosAI/automation (with content)
Read all step definitions in TalosAI/automation
Read the config files in TalosAI/automation
Search the solution for "WaitForSelectorState"
Write a new feature file named "SmokeCheck.feature" into TalosAI/automation/Features with a trivial one-scenario placeholder
```
