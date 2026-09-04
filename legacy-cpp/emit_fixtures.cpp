#include "quote_price.h"

#include <cstdio>

static void print_case(const char* id, const QuoteInput& input, const QuoteResult& r) {
    std::printf("  {\n");
    std::printf("    \"id\": \"%s\",\n", id);
    std::printf("    \"input\": {\n");
    std::printf("      \"markup_bps\": %d,\n", input.markup_bps);
    std::printf("      \"labor\": { \"minutes\": %d, \"rate_ore_per_hour\": %d },\n",
                input.labor.minutes, input.labor.rate_ore_per_hour);
    std::printf("      \"materials\": [\n");
    for (int i = 0; i < input.material_count; ++i) {
        const MaterialLine& m = input.materials[i];
        // A null sku is a real legacy input (resolve_unit_ore checks for it).
        // Emit JSON null, not "", so the fixture locks that branch honestly.
        if (m.sku) {
            std::printf("        { \"sku\": \"%s\", \"quantity\": %d, \"unit_ore\": %d }%s\n",
                        m.sku, m.quantity, m.unit_ore,
                        (i + 1 < input.material_count) ? "," : "");
        } else {
            std::printf("        { \"sku\": null, \"quantity\": %d, \"unit_ore\": %d }%s\n",
                        m.quantity, m.unit_ore,
                        (i + 1 < input.material_count) ? "," : "");
        }
    }
    std::printf("      ]\n");
    std::printf("    },\n");
    std::printf("    \"expected\": {\n");
    std::printf("      \"materials_ore\": %d,\n", r.materials_ore);
    std::printf("      \"labor_ore\": %d,\n", r.labor_ore);
    std::printf("      \"markup_ore\": %d,\n", r.markup_ore);
    std::printf("      \"vat_ore\": %d,\n", r.vat_ore);
    std::printf("      \"total_ore\": %d\n", r.total_ore);
    std::printf("    }\n");
    std::printf("  }");
}

int main() {
    std::printf("[\n");

    {
        MaterialLine lines[] = {{"NAIL-100", 2, 0}};
        QuoteInput in = {lines, 1, {60, 80000}, 1500};
        print_case("small_job_skips_markup", in, quote_price(in));
        std::printf(",\n");
    }
    {
        MaterialLine lines[] = {{"NAIL-100", 10, 0}, {"TIMB-2x4", 1, 0}};
        QuoteInput in = {lines, 2, {30, 80000}, 1500};
        print_case("volume_discount_then_markup", in, quote_price(in));
        std::printf(",\n");
    }
    {
        MaterialLine lines[] = {{"NAIL-100", 4, 99999}};
        QuoteInput in = {lines, 1, {0, 0}, 1000};
        print_case("price_book_overrides_line_unit", in, quote_price(in));
        std::printf(",\n");
    }
    {
        MaterialLine lines[] = {{"CUSTOM-X", 1, 20000}, {"SCREW-50", 2, 1}};
        QuoteInput in = {lines, 2, {0, 0}, 1500};
        print_case("cache_miss_uses_line_unit", in, quote_price(in));
        std::printf(",\n");
    }
    {
        MaterialLine lines[] = {{"SCREW-50", 1, 0}};
        QuoteInput in = {lines, 1, {7, 80000}, 2000};
        print_case("truncating_labor_and_vat", in, quote_price(in));
        std::printf(",\n");
    }
    {
        // Largest net that still fits 32-bit math in the VAT step:
        // 858,993 * 2500 = 2,147,482,500 <= INT_MAX. One more øre overflows.
        // The overflow region itself is deliberately NOT emitted here: signed
        // overflow is undefined behaviour in C++, so whatever this compiler
        // prints is an accident, not a business rule. The .NET port refuses
        // those inputs (HTTP 400); its own test covers that.
        MaterialLine lines[] = {{"CUSTOM-X", 1, 858993}};
        QuoteInput in = {lines, 1, {0, 0}, 0};
        print_case("largest_net_that_fits_int32", in, quote_price(in));
        std::printf(",\n");
    }

    // The cases below lock branches that quote_price.pseudocode.md documents but
    // nothing tested before: empty quote, non-positive quantities, the qty_sum == 3
    // boundary, negative markup, the two ways labor is zero, and a null sku.
    {
        // material_count 0 is an empty quote. Labor still prices; markup is 0 (qty_sum 0 < 3).
        QuoteInput in = {nullptr, 0, {30, 80000}, 1500};
        print_case("empty_materials_labor_only", in, quote_price(in));
        std::printf(",\n");
    }
    {
        // Zero and negative quantities contribute no money and do not count toward
        // qty_sum. The remaining line has qty 3, which is exactly the threshold:
        // qty_sum < 3 is false, so markup applies.
        MaterialLine lines[] = {{"NAIL-100", 0, 0}, {"TIMB-2x4", -5, 0}, {"SCREW-50", 3, 0}};
        QuoteInput in = {lines, 3, {0, 0}, 1500};
        print_case("non_positive_qty_skipped_and_qty_sum_three_gets_markup", in, quote_price(in));
        std::printf(",\n");
    }
    {
        // Negative markup_bps is not clamped; it reduces the net.
        MaterialLine lines[] = {{"SCREW-50", 4, 0}};
        QuoteInput in = {lines, 1, {0, 0}, -1000};
        print_case("negative_markup_not_clamped", in, quote_price(in));
        std::printf(",\n");
    }
    {
        // minutes > 0 but rate 0: labor stays 0.
        MaterialLine lines[] = {{"NAIL-100", 1, 0}};
        QuoteInput in = {lines, 1, {60, 0}, 1500};
        print_case("zero_rate_labor_is_zero", in, quote_price(in));
        std::printf(",\n");
    }
    {
        // rate set but minutes <= 0: labor stays 0.
        MaterialLine lines[] = {{"NAIL-100", 1, 0}};
        QuoteInput in = {lines, 1, {-30, 80000}, 1500};
        print_case("non_positive_minutes_labor_is_zero", in, quote_price(in));
        std::printf(",\n");
    }
    {
        // sku == nullptr: price book is skipped and the line's unit_ore is used.
        MaterialLine lines[] = {{nullptr, 2, 5000}};
        QuoteInput in = {lines, 1, {0, 0}, 1500};
        print_case("null_sku_uses_line_unit", in, quote_price(in));
        std::printf("\n");
    }

    std::printf("]\n");
    return 0;
}
