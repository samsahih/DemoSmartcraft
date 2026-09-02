# C++ to .NET Quote Engine — Proof of Method

This repository demonstrates a repeatable pipeline for moving legacy C++ business logic into a modern .NET 10 API **without changing the numbers**.

It is a functional proof of concept. **All tests pass.**

---

## The Core Strategy

1. **Extract:** Read the legacy C++ code to uncover real business rules—including hidden edge cases.
2. **Record:** Capture real C++ execution outputs as JSON (the single source of truth).
3. **Guard:** Write tests that lock the original outputs, prevent extra projects and layers we do not need, and enforce integer øre math.
4. **Implement:** Build a small, high-throughput .NET 10 API for this one feature that matches the C++ outputs down to the exact øre.

---

## What the Engine Does

Calculates HVAC and Plumbing job estimates using integer øre arithmetic (1 krone = 100 øre) matching legacy C++ behavior—no floating-point rounding or `decimal` conversions.

**Hidden business rules enforced by the test suite:**
* **Small Job Exemption:** Jobs with fewer than 3 items force markup to 0%.
* **Volume Discount:** Line items with 10+ items receive a 5% discount before markup is applied.
* **Materials-Only Markup:** Markup percentage applies strictly to materials, never labor.
* **Integer Truncation:** 25% VAT and labor calculations use C++ integer division (fractional øre are truncated toward zero, not rounded).

---

## Repository Structure

* `legacy-cpp/` — Original C++ pricing logic (`quote_price.cpp`) and fixture generator.
* `fixtures/quote-cases.json` — Recorded C++ results used for test assertions.
* `contracts/` — Extracted functional pseudocode and JSON specifications.
* `src/Smartcraft.Quotes/` — .NET 10 API for this feature (`Features/Quotes/CalculateQuote/`).
* `tests/Smartcraft.Quotes.Tests/` — Tests that check architecture and match the C++ results.

---

## How to Run

```bash
# Run all architecture and C++ parity tests
dotnet test

# Run the ASP.NET Core Web API host
dotnet run --project src/Smartcraft.Quotes
```

Leave that window running. Open a **second** PowerShell and call `POST /quotes/calculate`. Copy the `http://localhost:...` URL from the running window (typically `http://localhost:61785`). Use HTTP unless you already trust the HTTPS dev cert.

```powershell
Invoke-RestMethod `
  -Uri http://localhost:61785/quotes/calculate `
  -Method POST `
  -ContentType "application/json" `
  -Body '{
    "materials": [
      { "sku": "NAIL-100", "quantity": 2, "unitOre": 0 }
    ],
    "labor": { "minutes": 60, "rateOrePerHour": 80000 },
    "markupBps": 1500
  }'
```

That is a small job (2 items), so markup should be 0. Expected result:

```
materialsOre : 2500
laborOre     : 80000
markupOre    : 0
vatOre       : 20625
totalOre     : 103125
```
