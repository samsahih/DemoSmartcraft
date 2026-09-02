Proof-of-method analog: one C++ job-quote leaf → extraction contract. Not Smartcraft’s product, not a rewrite.

**Now:** Step 4 slice implementation. Calculator, handler, and `POST /quotes/calculate` live in `Features/Quotes/CalculateQuote/`. `dotnet test` is **green**.

**Step 1 — C++ demo** (`legacy-cpp/quote_price.cpp`): integer øre, in-memory price book (not a DB). If the cache is warm, line `unit_ore` is ignored. Hidden rules: qty sum `< 3` skips markup; line qty `>= 10` takes 5% off before markup; markup is materials only; every `/` truncates toward zero (including 25% VAT). Oracle: `legacy-cpp/compile-and-emit.bat` → `fixtures/quote-cases.json`. `truncating_labor_and_vat` is the case that punishes a C# rounding port.

**Step 2 — extraction**
- Pseudocode: [contracts/quote_price.pseudocode.md](contracts/quote_price.pseudocode.md)
- Records (slice only): `src/Smartcraft.Quotes/Features/Quotes/CalculateQuote/`
- JSON spec: [fixtures/quote-cases.json](fixtures/quote-cases.json)
- Compact contract: [contracts/QuotePrice.json](contracts/QuotePrice.json)

**Step 3 — NUnit harness** (`tests/Smartcraft.Quotes.Tests/`)

- **Architecture enforcement** (`Architecture/SliceArchitectureTests.cs`): reflection and `Directory.GetFiles` lock the slice namespace, keep money as `int` øre (not `decimal` / rounding), and ban extra layer projects (`Contracts`, `Domain`, `Application`, `Infrastructure`) plus EF/SQL/Testcontainers. Stops over-architecture and workspace pollution.
- **Oracle seam** (`CalculateQuote/QuoteCasesTests.cs`): cases are fed from `fixtures/quote-cases.json`. `Run()` calls `QuoteCalculator` with `InMemoryPriceBook`. `truncating_labor_and_vat` is the tripwire for a rounding port.
- **Zero type pollution:** JSON DTOs (`QuoteCaseDto` and friends) are `private sealed` inside the test class. Production `CalculateQuoteRequest` / `CalculateQuoteResponse` stay the only home for those types.

**Step 4 — .NET 10 slice** (`@02-slice-implementation.mdc`)

- **Price book:** `IPriceBook` / `InMemoryPriceBook` — process-lifetime SKU → øre map (`NAIL-100`, `TIMB-2x4`, `SCREW-50`). No SQL, EF, or `DbContext`.
- **Calculator:** `QuoteCalculator` — integer øre, C++-style `/` (truncate toward zero). Small-job markup skip, per-line volume discount, materials-only markup, 25% VAT.
- **Handler + endpoint:** `ICalculateQuoteHandler` orchestrates; `POST /quotes/calculate` is a thin adapter. `Smartcraft.Quotes` is a web host. Slice records and fixture expected values were not rewritten.
