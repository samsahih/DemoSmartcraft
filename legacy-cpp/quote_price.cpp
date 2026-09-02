#include "quote_price.h"

#include <map>
#include <string>

namespace {

std::map<std::string, int> g_price_book;
bool g_price_book_loaded = false;

void ensure_price_book() {
    if (g_price_book_loaded) {
        return;
    }
    g_price_book["NAIL-100"] = 1250;
    g_price_book["TIMB-2x4"] = 8900;
    g_price_book["SCREW-50"] = 450;
    g_price_book_loaded = true;
}

int resolve_unit_ore(const char* sku, int line_unit_ore) {
    ensure_price_book();
    if (sku == nullptr) {
        return line_unit_ore;
    }
    auto it = g_price_book.find(sku);
    if (it != g_price_book.end()) {
        return it->second;
    }
    return line_unit_ore;
}

int line_materials_ore(const MaterialLine& line) {
    if (line.quantity <= 0) {
        return 0;
    }
    int unit = resolve_unit_ore(line.sku, line.unit_ore);
    int ore = unit * line.quantity;
    // Undocumented: 5% volume discount when quantity >= 10, before markup.
    if (line.quantity >= 10) {
        ore = ore * 95 / 100;
    }
    return ore;
}

}  // namespace

void reset_price_book_for_tests() {
    g_price_book.clear();
    g_price_book_loaded = false;
}

QuoteResult quote_price(const QuoteInput& input) {
    QuoteResult result = {};

    int qty_sum = 0;
    for (int i = 0; i < input.material_count; ++i) {
        const MaterialLine& line = input.materials[i];
        if (line.quantity > 0) {
            qty_sum += line.quantity;
        }
        result.materials_ore += line_materials_ore(line);
    }

    if (input.labor.minutes > 0 && input.labor.rate_ore_per_hour != 0) {
        result.labor_ore = input.labor.minutes * input.labor.rate_ore_per_hour / 60;
    }

    int markup_bps = input.markup_bps;
    // Undocumented small-job exception: skip markup when total qty < 3.
    if (qty_sum < 3) {
        markup_bps = 0;
    }
    // Markup is on materials only, not labor.
    result.markup_ore = result.materials_ore * markup_bps / 10000;

    int net = result.materials_ore + result.labor_ore + result.markup_ore;
    result.vat_ore = net * 2500 / 10000;  // 25% VAT, truncate toward zero
    result.total_ore = net + result.vat_ore;
    return result;
}
