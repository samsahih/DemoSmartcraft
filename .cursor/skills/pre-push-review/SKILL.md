---
name: pre-push-review
description: >-
  Reviews the DemoSmartcraft codebase before any git push and decides READY or
  NOT READY. Use whenever the user says push, ready to push, ship it, publish,
  or asks for a pre-push review, and whenever the pre-push-gate hook blocks a
  git push. On READY it writes .cursor/push-approval.json and pushes. On NOT
  READY it stops, lists what must be fixed in plain language, and waits for the
  human to choose the next step.
---

# Pre-push review

You are the last check before code leaves this machine. The human makes the
decisions; you make them informed. Never push on your own judgement when the
verdict is NOT READY, and never fix things silently to force a READY.

## Context you must know

This repo ports a legacy C++ pricing function (`legacy-cpp/quote_price.cpp`)
to a .NET 10 API (`src/Smartcraft.Quotes/Features/Quotes/CalculateQuote/`).
The contract is **the C# must produce the same integer øre as the C++**.
The C++ answers are recorded in `fixtures/quote-cases.json` and are the only
source of truth for expected numbers. Guardrails live in `.cursor/rules/`.

## Procedure

Work through every step. Do not skip a step because the change "looks small".

### 1. Establish what is being pushed

- `git status --short` — if there are uncommitted changes, stop and ask the
  human to commit or discard them first. The review is of a commit, not of a
  working tree. (If `git` is not on PATH, locate it before continuing: on
  Windows, `Get-Command git` and, failing that, the copy bundled with Git for
  Windows or Visual Studio under `...\Team Explorer\Git\cmd\git.exe`. Do not
  hard-code a machine-specific path into this file.)
- `git rev-parse HEAD` and `git log --oneline @{upstream}..HEAD` to list the
  commits that would go out. If there is no upstream, review everything.
- `git diff @{upstream}...HEAD --stat` then read the full diff. Read every
  changed file completely, not only the diff hunks.

### 2. Build and test

- `dotnet build` with zero warnings that were not already present.
- `dotnet test`. Every test must pass. A skipped or deleted test is a blocker
  unless the commit message explains why.

### 3. Parity with the C++ oracle

Only when anything under `legacy-cpp/`, `fixtures/`, or `QuoteCalculator.cs`
changed:

- If C++ or `emit_fixtures.cpp` changed, regenerate the fixture
  (`legacy-cpp/compile-and-emit.bat`) and confirm `fixtures/quote-cases.json`
  is byte-identical to the committed one. A diff is a blocker: either the
  fixture was hand-edited or the generator drifted.
- Read `QuoteCalculator.cs` line by line against `quote_price.cpp`. Same
  branches, same order, same integer truncation. Any deviation must be a
  deliberate, documented decision (for example an overflow policy), and a
  fixture or test must lock it.
- Every `id` in `fixtures/quote-cases.json` must have a matching
  `[TestCase]` in `QuoteCasesTests.cs`. An orphaned fixture row is a row that
  runs nowhere.
- Expected values are never typed by hand. If a new expected number appeared
  without a C++ change that produced it, that is a blocker.

### 4. Silent wrong answers

Look for ways the API returns 200 with a wrong number. This is the worst
class of bug in this repo because nothing downstream will notice.

- 32-bit `int` overflow in any multiply-before-divide (`net * 2500`,
  `materials * markup_bps`, `minutes * rate`). Is it guarded (`checked`,
  explicit range check) or documented as accepted?
- Null request shapes (`materials`, `labor`, `sku`) that throw instead of
  returning a 400.
- Nullability annotations that lie about what JSON can send.

### 5. Guardrails from `.cursor/rules/`

- Vertical slice only: no `Contracts`, `Domain`, `Application`,
  `Infrastructure` projects or folders.
- Money stays `int` øre. No `decimal`, `double`, `long` on slice records.
- No EF, SQL, `DbContext`, Testcontainers.
- Endpoint is a thin adapter; math lives in the calculator.
- If a rule was changed in the same commit as code that would have violated
  it, call that out explicitly. Changing the rule to fit the code is a
  decision for the human, not a fix.

### 6. Tests protect the change

- New behaviour has a test that would fail without it.
- New or changed HTTP surface has at least one test that goes through the
  real endpoint (JSON binding, routing, DI), not only the calculator.
- Architecture tests still cover the thing they claim to cover.

### 7. Hygiene

- No secrets, connection strings, tokens, or local absolute paths in
  committed files.
- No leftover debugging (`Console.WriteLine`, commented-out code, `TODO`
  added without an issue reference).
- `.gitignore` still excludes `bin/`, `obj/`, `TestResults/`, and
  `.cursor/push-approval.json`.
- README still matches how the code actually runs.

## Classify every finding

**Blocker** (push is NOT READY):
- Build or any test fails.
- C# and C++ disagree on any fixture, or a fixture was hand-edited.
- A path where the API returns a success response with a wrong number and
  nothing guards or documents it.
- A `.cursor/rules/` guardrail is violated, or an architecture test was
  removed or weakened.
- Secrets or credentials in the commit.

**Should fix** (push can go ahead; list them so they are not forgotten):
- Missing tests for a change that is otherwise correct.
- Nullability or validation gaps that produce a 500 instead of a 400.
- Duplicated code, dead code, naming.

**Note** (information only):
- Hygiene items, docs drift, style.

## Report format

Write for a reader who did not write the code and does not know the jargon.
Every technical term gets one plain sentence saying what it means and why it
matters. Short paragraphs. No walls of bullets.

```
## Pre-push review — <short sha> on <branch>

**Verdict: READY** | **Verdict: NOT READY**

One or two sentences saying what this push contains and the overall picture.

### Blockers
(Only when NOT READY. One entry per problem.)

**1. <Plain title>**
What is wrong, in one or two sentences a non-specialist can follow.
Why it matters: what goes wrong for a user or for the numbers if this ships.
Where: `path/to/file.cs` line N.
Fix: the smallest change that makes it go away, and the test that proves it.

### Should fix (does not block this push)
Same shape, shorter.

### Notes
One line each.

### What I checked
Build: pass/fail. Tests: N passed, M failed. Fixture regenerated: yes/no/not needed.
Files read in full: list.
```

## Decision protocol

**If NOT READY:**
1. Do not push. Do not write `.cursor/push-approval.json`.
2. Do not start fixing anything.
3. Print the report and end the turn with exactly one question: which
   blocker the human wants to tackle first, or whether they want to push
   anyway (in which case they must say so explicitly, and you record that in
   the approval file under `humanOverride`).
4. When the human picks an item, fix only that item, run `dotnet test`,
   commit, then re-run this whole review from step 1. Every new commit
   invalidates the previous approval by design.

**If READY:**
1. Write `.cursor/push-approval.json` at the repo root with this shape:
   ```json
   {
     "commit": "<full 40-char sha of HEAD>",
     "branch": "<branch>",
     "verdict": "READY",
     "reviewedAt": "<ISO-8601 timestamp>",
     "shouldFix": ["<one line per should-fix item>"],
     "humanOverride": null
   }
   ```
2. Print the report.
3. Run `git push`. The `pre-push-gate` hook will verify the approval matches
   HEAD and then ask the human to confirm. That confirmation is the human's
   decision; do not try to bypass or re-run the push if they decline.
4. After a successful push, delete `.cursor/push-approval.json`.

## Things you must never do in this skill

- Change `fixtures/quote-cases.json` by hand.
- Delete, skip, or loosen a test to reach READY.
- Edit `.cursor/rules/` to make a violation disappear.
- Write the approval file for a commit you did not fully review.
- Push with `--force`, `--no-verify`, or to a branch other than the one
  the human named.
