# End-to-End: User Story → Generated Test → Multi-Layer Validation

This is the flagship demo flow: a user story goes in, a compiled, running,
validated test comes out. It chains the Solution Writer tools (code
generation) with the Playwright/API/Database tools (execution) and the
Reporting tools (proof).

Works from either entry point — the Agent UI (`http://localhost:8080`) or
GitHub Copilot Chat in Agent mode referencing
`.github/agents/story-to-test.agent.md`. See `docs/SHOWCASE-GUIDE.md` for
which one to use and why.

## Step 1 — Bring in the story

If you have Azure DevOps configured, ask for it directly:
```
Get user story #1234 from Azure DevOps and generate BDD test artifacts for it.
```

Otherwise paste one directly (this is the input `story-to-test.agent.md` expects):
```
Convert this user story to BDD tests:

Title: Add item to cart
As a shopper
I want to add a product to my cart
so that I can purchase it later

AC1: Clicking "Add to cart" on a product adds it and updates the cart badge count
AC2: Adding the same product twice is not possible — the button becomes "Remove"
AC3: Removing an item from the cart decreases the badge count
AC4: The cart badge is hidden (not "0") when the cart is empty
```

Expect three generated files, following the project's existing conventions
(read from the real files, not invented): a `.feature` file under
`TalosAI/automation/Features`, a `*Steps.cs` under `TalosAI/automation/Steps`,
and a `*Page.cs` under `TalosAI/automation/Pages` — each AC mapped to at least
one positive and one negative scenario, tagged per the convention in
`story-to-test.agent.md` (`@story-<id>`, `@smoke`/`@regression`, `@negative`,
`@api`/`@ui` as relevant).

## Step 2 — Review before running

```
Read back the feature file, step definitions, and page object you just generated.
```
This is the natural pause point in a real demo — show the panel/reviewer the
generated Gherkin and code before executing anything.

## Step 3 — Execute and validate across layers

```
Build the solution, then run the scenario(s) you just generated. Report the
pass/fail result for each scenario.
```

If the story touches more than the UI (e.g. an AC implies a badge count is
persisted, or an API confirms cart state), extend the ask explicitly — the
agent won't infer cross-layer checks you didn't ask for:
```
Also confirm via the API that reqres.in still responds correctly (smoke-check
the environment) and log this whole run as a named test ("Cart Smoke - Story
1234") with a step log and a final report.
```

## Step 4 — Produce the artifact you'd actually show someone

```
Generate the Allure/Extent report for this run and tell me where the HTML
output was written.
```

## What "good" looks like at each step

| Step | Signal it worked |
|---|---|
| 1 | Three files exist on disk, one Scenario per AC minimum, tags applied |
| 2 | The read-back content matches what's on disk — no drift between what the agent claims it wrote and what's actually there |
| 3 | `dotnet build` succeeds with 0 errors; each scenario reports Passed/Failed (not skipped, not erroring on missing step bindings) |
| 4 | A real HTML report file exists at the path the agent gives you, and opening it shows the scenario(s) you just ran |

If step 3 shows a missing step binding or a compile error, that's the
generated code not matching the real project's conventions — the fix is to
tighten the prompt in Step 1 (point at a specific existing Steps.cs/Page.cs
as the pattern to copy) and regenerate, not to hand-patch the output.
