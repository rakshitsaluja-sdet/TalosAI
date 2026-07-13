# Combined Tool Test Prompts

Where the individual-tool prompts (`tool-tests-individual.md`) prove each tool
works on its own, these prove the agent can **chain tools across categories**
in a single natural-language ask — the actual pattern used by the E2E workflow
in `e2e-story-to-test.md`. Each prompt below is a single message; the agent
should plan and call multiple MCP tools in sequence to satisfy it.

## UI + API

```
Log in to https://www.saucedemo.com/ as standard_user/secret_sauce, confirm the
inventory page loaded, then separately call GET https://reqres.in/api/users/2
and confirm the API returns a 200 with a "data" field. Report both results.
```

## API + Performance

```
Configure the API base URL as https://reqres.in. First send a single GET request
to /api/users and assert it returns 200 with the correct fields present
(functional check). Then run a 20-second load test with 5 concurrent users
against the same endpoint and report throughput and error rate
(performance check). Tell me whether both the functional and performance
checks passed.
```

## UI + DB + API (three-way)

```
Create a new user named "Grace Hopper" via POST https://reqres.in/api/users.
Confirm the response is 201 and capture the returned id. Insert a matching
row into the local SQLite "SyncedUsers" table (Id, Name, CreatedAt). Then
verify via a database query that exactly one row exists for that id. Finally,
open https://www.saucedemo.com/ and confirm the login page loads normally
(standing in for "the UI layer is unaffected/healthy" in a real 3-tier check).
Summarize pass/fail for the API step, the DB step, and the UI step separately.
```

## Performance alone (isolation check)

```
Configure a performance test against https://reqres.in. Run a load test
(10 users, 15 seconds), then a stress test (ramping 5 to 30 users over 30
seconds), then a spike test (sudden burst to 50 users for 10 seconds). Export
one combined performance report and tell me which of the three profiles had
the highest error rate.
```

## UI + Reporting

```
Log in to https://www.saucedemo.com/, add "Sauce Labs Backpack" to the cart,
and confirm the cart badge shows 1. Wrap this in a named test run: start a
test called "Cart Smoke", log a step after each action, end the test as
passed or failed based on the actual outcome, then generate the report.
```

## API + Database + Reporting (mirrors UserSyncE2E.feature)

```
Capture the current row count in the local "SyncedUsers" table. Create a user
via POST https://reqres.in/api/users, sync it into SyncedUsers, and confirm
the count increased by exactly one. Log each of those three steps into a
named test run and end it with the correct pass/fail status.
```
