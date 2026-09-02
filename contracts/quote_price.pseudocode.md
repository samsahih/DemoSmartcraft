# quote_price — business logic (from `legacy-cpp/quote_price.cpp`)

Money is **integer øre** (1/100 NOK). Every `/` is C++ integer division: **truncate toward zero**. There is no `decimal`, no technician grade, no after-hours surcharge.

Process-lifetime state (not a database): a static in-memory price book, lazy-loaded once per process.

```
PRICE_BOOK (loaded once, then reused):
  "NAIL-100" → 1250
  "TIMB-2x4" → 8900
  "SCREW-50" → 450
```

## quote_price(input) → result

1. Start with all result fields at 0. `qty_sum = 0`.

2. For each material line `i` in `0 .. material_count-1`:
   - If `quantity > 0`, add `quantity` to `qty_sum`.
     (Zero and negative quantities do **not** count toward the small-job check.)
   - Add `line_materials_ore(line)` to `result.materials_ore`.

3. Labor, only when `minutes > 0` **and** `rate_ore_per_hour != 0`:
   ```
   labor_ore = minutes * rate_ore_per_hour / 60
   ```
   Otherwise labor stays 0. A non-zero rate with `minutes <= 0` still yields 0 labor.

4. Markup basis:
   ```
   markup_bps = input.markup_bps
   if qty_sum < 3:          // undocumented small-job exception
       markup_bps = 0
   markup_ore = materials_ore * markup_bps / 10000
   ```
   Markup is on **materials only**. Labor is never marked up.
   `1500` bps = 15%. Negative `markup_bps` is not clamped.

5. VAT and total:
   ```
   net       = materials_ore + labor_ore + markup_ore
   vat_ore   = net * 2500 / 10000    // 25% VAT, truncate toward zero
   total_ore = net + vat_ore
   ```

## line_materials_ore(line)

1. If `quantity <= 0`, return 0 (line is skipped for money).
2. Resolve unit price:
   - `sku == null` → use `line.unit_ore`.
   - `sku` found in price book → use the **book** price; `line.unit_ore` is ignored.
   - `sku` not in book → use `line.unit_ore` (cache miss).
3. `ore = unit * quantity`.
4. Undocumented volume discount, **per line, before markup**:
   if `quantity >= 10` then `ore = ore * 95 / 100` (5% off, integer).
5. Return `ore`.

## Hidden assumptions / magic numbers

| Item | Value | Notes |
|---|---|---|
| Volume discount threshold | quantity `>= 10` | Per line, not quote total |
| Volume discount | `* 95 / 100` | 5% off; truncates |
| Small-job exception | `qty_sum < 3` | Forces markup to 0 |
| Markup scale | `/ 10000` | basis points |
| VAT | `* 2500 / 10000` | 25%, always applied, even if net is 0 |
| Price book | 3 SKUs, static | Warm cache makes `unit_ore` a lie |
| Pointer + length | `materials` + `material_count` | No bounds check; count 0 is an empty quote |

`reset_price_book_for_tests()` clears the map and the loaded flag so tests can re-seed the book.
