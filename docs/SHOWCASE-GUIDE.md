# TalosAI — Demo & Showcase Runbook

A step-by-step operating guide for running and presenting TalosAI: an
agentic QA framework where an LLM agent takes a user story and produces a
compiled, executable, multi-layer-validated test — not just a code snippet.

Use this document as your script if you're demoing the framework to an
employer, a hiring panel, or leadership. It answers the question "what do I
actually click/type, in what order?" precisely, because that's the part that
falls apart in a live demo if it isn't nailed down beforehand.

---

## 0. One-time setup (do this before the room fills up)

```powershell
git clone <your-fork-url>
cd TalosAI-repo

# C# test suite
cd TalosAI
copy automation\config.template.properties automation\config.properties
dotnet restore
dotnet build
cd ..

# MCPBridge (no setup needed beyond dotnet restore, done above at solution level)

# Python MCP server + agent
cd mcp_server
pip install -r requirements.txt
cd ../agent
pip install -r requirements.txt
cd ..

# Node MCP server (only if you want the lighter playwright-only path too)
cd playwright-mcp
npm install
npx playwright install chromium
cd ..
```

You'll also need **one** of:
- The Claude Code CLI installed and signed in (`npm install -g @anthropic-ai/claude-code`, then run `claude` once — no API key needed, it reuses your existing Claude account session), **or**
- Ollama running locally (pass `-UseOllama` to `start_all.ps1` for a fully offline demo)

---

## 1. Two ways to run this — pick one before you start

There are genuinely two independent, working entry points. Don't mix them up
mid-demo — decide beforehand which story you're telling.

### Path A — Standalone Agent UI (fastest to show, no IDE needed)

A browser-based chat UI, purpose-built for this project, driving MCPBridge
directly through a Python agent loop.

```powershell
cd agent
.\start_ui.ps1
```

What this actually does: checks if MCPBridge (`localhost:5555/health`) is
already up; if not, it runs `..\restart_mcpbridge.ps1` for you. Then it
starts `agent_web_server.py`, which opens `http://localhost:8080` in your
browser automatically. Type a prompt in the box, hit send — the agent plans,
calls MCP tools against MCPBridge, and streams back a result.

This is the whole thing. You do **not** need to separately run
`start_all.ps1` for this path — `start_ui.ps1` brings up everything Path A
needs on its own. (`start_all.ps1` is a broader launcher — MCPBridge + the
standalone Python MCP server + the agent runner as background jobs — useful
if you want the Python MCP server available for some *other* MCP client at
the same time. It is not a prerequisite for the Agent UI.)

### Path B — GitHub Copilot, Agent Mode, in VS Code (IDE-native)

No Python agent process at all — Copilot itself calls the MCP tools.

**Prerequisites for this path specifically:**
1. MCPBridge running (`cd MCPBridge && dotnet run`, or via `start_all.ps1`)
2. The Python MCP server running (`cd mcp_server && python server.py`) —
   MCPBridge itself is a plain REST API, not an MCP server; `mcp_server`
   is what speaks real MCP to Copilot on its behalf.
3. `.vscode/mcp.json` (already checked in) registers both `talosai-mcpbridge`
   (the full tool suite, via `mcp_server`) and `talosai-playwright` (a
   lighter, Playwright-only alternative, via `playwright-mcp/server.js` —
   run `node playwright-mcp/server.js` if you want this one too, it's
   optional).
4. **Copilot Chat must be in Agent mode**, not Ask or Edit mode — MCP tools
   are only callable from Agent mode. Confirm the two servers show up in the
   tools picker (🔧 icon) before you start talking.

To invoke a specific persona, reference its file directly in chat, e.g.:
```
#file:.github/agents/story-to-test.agent.md

Convert this user story to BDD tests: <paste story>
```

---

## 2. The showcase script

This is the flow worth actually presenting — story in, validated test out.
Full prompt text for each step lives in `agent/prompts/e2e-story-to-test.md`;
this section is the narration to go with it.

1. **"Here's a user story, unmodified from Azure DevOps."** Paste it (or
   pull it live with `Get user story #1234 from Azure DevOps`, if you've
   configured `configure_azure_devops` beforehand with a real PAT).
2. **"Watch it design the test, not just write code."** The agent maps each
   acceptance criterion to at least one positive and one negative scenario,
   picks tags, and — this is the part worth pausing on — matches the
   *existing* project's conventions (namespace, base class, Gherkin style)
   because it read the real files first, rather than inventing its own style.
3. **"Three files, not one."** Show the generated `.feature`, `Steps.cs`, and
   `Page.cs` side by side. This is the difference between "an LLM wrote some
   Gherkin" and "an LLM produced a compilable, runnable artifact."
4. **"Now it proves it actually works — build and run, not just generate."**
   Ask it to build the solution and run the new scenario(s). This is the
   moment that separates this from a code-completion demo: a real `dotnet
   build`, a real browser driving saucedemo.com (or your real app), a real
   pass/fail.
5. **"And here's the evidence."** Generate the Allure/ExtentReports HTML
   report and open it. This is what you leave behind — a report, not a
   transcript.

Optional escalation if you have more time: ask for a **combination** check
(`agent/prompts/tool-tests-combinations.md`) — e.g. validate the same story's
outcome through the UI *and* confirm the corresponding API/DB state changed.
This is the "it's not just clicking buttons" moment.

---

## 3. Tool coverage reference

If someone asks "what can it actually do" and you want to demonstrate
breadth rather than depth:

- `agent/prompts/tool-tests-individual.md` — one section per tool category
  (Playwright, Selenium, API, Database, Performance, Test Data, Image,
  Reporting, Azure DevOps, SpecFlow, Solution Reader/Writer), each a ready
  copy-paste prompt sequence.
- `agent/prompts/tool-tests-combinations.md` — the cross-category chains
  (UI+API, API+Performance, UI+DB+API, Performance in isolation) that prove
  the agent can plan across tool boundaries, not just call one tool per ask.

---

## 4. If something doesn't come up

| Symptom | Likely cause / fix |
|---|---|
| Agent UI says "MCPBridge not available" | `MCPBridge` isn't running — `cd MCPBridge && dotnet run`, wait for `http://localhost:5555/health` to return `ok` |
| Copilot's tool picker doesn't show `talosai-*` servers | `mcp_server`/`playwright-mcp` aren't running, or Copilot Chat isn't in **Agent mode** |
| Generated step definitions don't compile | Re-run with a more specific prompt pointing at an existing `Steps.cs`/`Page.cs` as the pattern to match, rather than hand-editing the output |
| `execute_non_query`/raw SQL tool calls get rejected | Intentional — add `allow_write: true` explicitly; this framework treats DB writes as opt-in, not default |
| Browser launches but tests fail on a real corporate app instead of the demo targets | Update `config.properties` `BaseUrl`/`ApiBaseUrl` — the checked-in template points at public demo services on purpose |
