#pragma once

// Legacy job-quote leaf. Money is integer øre (1/100 NOK). Pointer + length
// for material lines; process-lifetime price book in static storage.

struct MaterialLine {
    const char* sku;
    int quantity;
    int unit_ore;  // ignored when sku is in the in-memory price book
};

struct LaborLine {
    int minutes;
    int rate_ore_per_hour;
};

struct QuoteInput {
    MaterialLine* materials;
    int material_count;
    LaborLine labor;
    int markup_bps;  // 1500 = 15%
};

struct QuoteResult {
    int materials_ore;
    int labor_ore;
    int markup_ore;
    int vat_ore;
    int total_ore;
};

QuoteResult quote_price(const QuoteInput& input);
void reset_price_book_for_tests();
