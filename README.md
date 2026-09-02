Proof-of-method analog: one C++ job-quote leaf → extraction contract. Not Smartcraft’s product, not a rewrite.

**Now:** Step 1 (legacy C++) and Step 2 (extraction). No HTTP host, calculator, or tests yet.

**Step 1 — C++ demo** (`legacy-cpp/quote_price.cpp`): integer øre, in-memory price book (not a DB). If the cache is warm, line `unit_ore` is ignored. Hidden rules: qty sum `< 3` skips markup; line qty `>= 10` takes 5% off before markup; markup is materials only; every `/` truncates toward zero (including 25% VAT). Oracle: `legacy-cpp/compile-and-emit.bat` → `fixtures/quote-cases.json`. `truncating_labor_and_vat` is the case that punishes a C# rounding port.

**Step 2 — extraction**
- Pseudocode: [contracts/quote_price.pseudocode.md](contracts/quote_price.pseudocode.md)
- Records (slice only): `src/Smartcraft.Quotes/Features/Quotes/CalculateQuote/`
- JSON spec: [fixtures/quote-cases.json](fixtures/quote-cases.json)
- Compact contract: [contracts/QuotePrice.json](contracts/QuotePrice.json)

Agent for this step: `.cursor/rules/01-extraction-agent.mdc`. Next git step is the vertical-slice port (handler, calculator, endpoint, NUnit).
