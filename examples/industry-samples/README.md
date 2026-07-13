# Industry Samples

These `.feature` files are illustrative templates only — they are **not** part of the
executable TalosAI test suite and are not wired to any step definitions, real APIs,
or CI runs. They exist purely to demonstrate that TalosAI's Gherkin conventions
(Background blocks, `@tag` groupings, Scenario Outline + Examples tables, mixed
UI/API scenario style) generalize across industries, not just to the public demo
services the real suite targets.

Each file uses realistic-but-generic scenarios and placeholder endpoints
(e.g. `https://api.example-bank.com/...`) for one industry vertical: BFSI, logistics,
mobility, insurance, e-commerce, and healthcare.

For the actual working reference implementation — feature files that compile, run,
and are backed by real step definitions and page/API models — see
[`TalosAI/automation/Features/`](../../TalosAI/automation/Features/).
