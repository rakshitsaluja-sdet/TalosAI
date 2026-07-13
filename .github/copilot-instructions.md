# TalosAI QA Automation — Copilot Global Instructions
 
## Project Identity
You are assisting a **Senior QA Automation Architect** working on
**TalosAI**, an open-source agentic QA automation framework. Its example
suite targets public demo services (saucedemo.com for UI, reqres.in for
API) so the whole repo is runnable by anyone who clones it.

## Technology Stack
- Language:      C# .NET 9
- Test Framework: SpecFlow (BDD)
- UI Automation: Selenium WebDriver 4.x + Playwright (via MCP)
- API Testing:   RestSharp
- IDE:           Visual Studio 2022
- CI/CD:         GitHub Actions
- AI Layer:      GitHub Copilot + MCPBridge + playwright-mcp
- Reporting:     ExtentReports / Allure
 
## Solution Structure
```
TalosAI-repo/
├── TalosAI/
│   ├── automation/
│   │   ├── Features/           ← .feature files (Gherkin): Login, Inventory,
│   │   │                          ProductCatalog, Checkout, UsersApi,
│   │   │                          UserSyncE2E, ComponentShowcase, ...
│   │   ├── Steps/               ← [Binding] step definition classes
│   │   ├── Pages/               ← Page Objects (Selenium + Playwright)
│   │   ├── Hooks/               ← BeforeScenario, AfterScenario
│   │   ├── Models/              ← DTOs, request/response models
│   │   └── TDM/                 ← test data builders/cleanup
│   └── core/                    ← BaseTest, ApiClient, PlaywrightDriver, ...
├── MCPBridge/                   ← AI tool bridge (ASP.NET Core, C#)
├── mcp_server/                  ← Python MCP server (SDK-based, SSE)
├── playwright-mcp/              ← Playwright MCP server (Node.js)
│   └── server.js                ← real MCP tools over Streamable HTTP
├── agent/                       ← standalone Python agent runner + UI
└── .github/
    ├── copilot-instructions.md
    └── agents/                  ← QA agent persona files
        ├── story-to-test.agent.md
        ├── self-healing.agent.md
        ├── code-review.agent.md
        ├── regression.agent.md
        └── api-testing.agent.md
```

## Coding Standards — Always Follow These
- Namespaces match folder structure: `TalosAI.Automation.Pages`, `TalosAI.Automation.Steps`
- Page objects use constructor injection for `IPage` (Playwright) or `IWebDriver` (Selenium)
- All waits use `WebDriverWait` or Playwright's built-in waits — no `Thread.Sleep` ever
- Step definitions inherit from `BaseSteps` if it exists
- Feature files use British English, sentence case tags: `@smoke`, `@regression`
- Every scenario has at least one `@tag`
- RestSharp clients inherit from `BaseApiClient` if it exists
- No hardcoded URLs — always read from `config.properties` / `config.template.properties`
- No hardcoded test data — use test data builders or Bogus library
- Screenshots taken on every failure via AfterScenario hook
 
## Gherkin Style Rules
- Feature title: matches user story title exactly
- Scenarios: Given/When/Then — no And at the start of a scenario
- Background: used when 3+ scenarios share setup steps
- Scenario Outline: used when same flow needs multiple data sets
- Tags always on the line above the Scenario keyword
- Maximum 8 steps per scenario — split if more needed
 
## What You Must Never Do
- Never use `Thread.Sleep` — use explicit waits
- Never hardcode credentials in test files
- Never create duplicate step definitions
- Never use `driver.FindElement` without a wait wrapper
- Never ignore exceptions silently
- Never create a step definition without a corresponding feature step