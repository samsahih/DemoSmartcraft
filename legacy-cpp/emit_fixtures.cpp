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
        std::printf("        { \"sku\": \"%s\", \"quantity\": %d, \"unit_ore\": %d }%s\n",
                    m.sku ? m.sku : "", m.quantity, m.unit_ore,
                    (i + 1 < input.material_count) ? "," : "");
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
        std::printf("\n");
    }

    std::printf("]\n");
    return 0;
}
