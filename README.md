# TalosAI

**TalosAI** is an agentic, self-healing QA automation framework. It pairs a
C# SpecFlow/Selenium/Playwright test suite with an MCP (Model Context
Protocol) tool layer, so an LLM agent can drive browsers, call APIs, query
databases, and read/write test code — all through the same tool interface
an IDE-integrated AI assistant (Claude, GitHub Copilot, etc.) already speaks.

The example test suite runs entirely against public demo services
([saucedemo.com](https://www.saucedemo.com/) for UI,
[reqres.in](https://reqres.in/) for API, and
[the-internet.herokuapp.com](https://the-internet.herokuapp.com/) for
tricky UI patterns), plus a local SQLite file for a self-contained
API-to-database sync example — clone it and everything runs, no internal
services or credentials required.

## Architecture

```
TalosAI/            C# test suite: SpecFlow + NUnit, Selenium + Playwright
├── automation/      Feature files, Page Objects, Steps, Hooks, test data
└── core/            BaseTest, ApiClient, PlaywrightDriver, config helpers

MCPBridge/           ASP.NET Core MCP tool router (C#)
                      Browser automation, API testing, database queries,
                      image comparison, reporting, solution read/write —
                      all exposed as MCP tools with Playwright-primary /
                      Selenium-fallback execution and selector self-healing.

mcp_server/           Standalone Python MCP server (official `mcp` SDK, SSE)
                      Proxies tool calls through to MCPBridge.

playwright-mcp/       Standalone Node MCP server (Streamable HTTP)
                      A lighter-weight Playwright-only tool set.

agent/                Python agent runner + browser UI
                      Drives the MCP tools from natural-language prompts.
```

An agent (or you, directly) calls a tool like `navigate` or `click_element`
through whichever MCP server is running; MCPBridge tries Playwright first,
falls back to Selenium on UI failures (with self-healing selector retry in
between), and reports results back through the same MCP response shape
regardless of which engine actually handled it.

## Quickstart — run the test suite

```powershell
git clone <your-fork-url>
cd TalosAI-repo/TalosAI
copy automation\config.template.properties automation\config.properties
dotnet restore
dotnet build
dotnet test --filter "Category=login"
```

`config.template.properties` already points at the public demo targets, so
the copy above works with no edits. Override `BaseUrl` / `ApiBaseUrl` /
`Browser` / `Headless` in your local `config.properties` (gitignored) if
you want to point the suite elsewhere.

Feature areas in `TalosAI/automation/Features/`:

| Feature | Target | Demonstrates |
|---|---|---|
| `Login.feature` | saucedemo.com | Valid/invalid/locked-out sign-in |
| `Inventory.feature` | saucedemo.com | Listing + sort-order validation |
| `ProductCatalog.feature` | saucedemo.com | Cart add/remove/contents |
| `Checkout.feature` | saucedemo.com | Required-field validation, totals |
| `UsersApi.feature` | reqres.in | REST GET + pagination assertions |
| `UserSyncE2E.feature` | reqres.in + local SQLite | API-to-database sync pattern |
| `ComponentShowcase.feature` | the-internet.herokuapp.com | Playwright, explicit waits, tricky DOM patterns |

## Running the MCP tool layer

Fastest path — the standalone Agent UI:

```powershell
cd agent
.\start_ui.ps1   # auto-starts MCPBridge if needed, opens http://localhost:8080
```

Or drive it from GitHub Copilot Chat directly in VS Code (Agent mode) —
`.vscode/mcp.json` already registers the MCP servers. Both paths, plus the
full story-to-test demo flow and ready-to-paste prompts for every tool
category, are documented step by step in
**[docs/SHOWCASE-GUIDE.md](docs/SHOWCASE-GUIDE.md)** — start there if you're
presenting this or just want the fastest route to "it's working."

To run each service individually:

```powershell
cd MCPBridge && dotnet run              # http://localhost:5555
cd mcp_server && pip install -r requirements.txt && python server.py   # SSE on :8765
cd playwright-mcp && npm install && node server.js                     # Streamable HTTP on :3000
cd agent && pip install -r requirements.txt && python agent_web_server.py
```

`start_all.ps1` starts MCPBridge + the Python MCP server + the agent runner
together as background jobs — useful if you want everything up for both
Copilot and the Agent UI at once, but not required for either individually.

## Repository layout

- `docs/SHOWCASE-GUIDE.md` — full demo runbook (setup, both agent entry points, the story-to-test showcase script)
- `agent/prompts/` — ready-to-paste prompts: every tool individually, cross-category combinations, and the full story-to-test flow
- `examples/industry-samples/` — illustrative (non-executable) Gherkin templates showing the framework's conventions applied to BFSI, logistics, mobility, insurance, e-commerce, and healthcare
- `.github/agents/` — Copilot agent persona prompts (regression, API testing, self-healing, etc.)
- `.github/workflows/test-automation.yml` — CI: builds and runs the suite on `windows-latest`
- `.editorconfig` — shared C#/Python/JS style rules

## Contributing

Issues and PRs welcome. Please run `dotnet build` and the relevant
`dotnet test --filter` before submitting, and keep new example scenarios
targeting public, stable demo services so the suite stays runnable for
everyone.
