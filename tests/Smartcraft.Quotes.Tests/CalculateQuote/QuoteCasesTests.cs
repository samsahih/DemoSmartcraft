using System.Reflection;
using System.Text.Json;
using Smartcraft.Quotes.Features.Quotes.CalculateQuote;

namespace Smartcraft.Quotes.Tests.CalculateQuote;

/// <summary>
/// Characterization tests for quote pricing.
///
/// the C++ program already ran these inputs and wrote the answers to
/// <c>fixtures/quote-cases.json</c>. These tests load that file and check that C#
/// produces the same numbers. They do not re-read C++ and they do not invent
/// expected values — if a number is wrong, the fixture is the oracle, not a
/// new rounding rule in C#.
///
/// Expected øre come from the JSON. Do not change the JSON to make tests green.
/// </summary>
public sealed class QuoteCasesTests
{
    // JSON keys are snake_case (materials_ore). Slice records are PascalCase (MaterialsOre).
    // SnakeCaseLower maps them one to one, so the fixture deserializes straight into
    // the slice records. There is no second DTO tree in this file.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Sanity check that we are still pointed at the real oracle file, not a
    /// forked copy. <c>truncating_labor_and_vat</c> is the case that fails if
    /// someone ports money as <c>decimal</c> or rounds instead of truncating
    /// toward zero (C++ integer division).
    /// </summary>
    [Test]
    [Description("The oracle file is present, non-empty, and contains the truncation tripwire.")]
    public void Fixture_file_is_the_oracle_and_includes_the_truncation_tripwire()
    {
        var ids = LoadCases().Select(c => c.Id).ToList();

        Assert.That(ids, Is.Not.Empty);
        Assert.That(ids, Is.Unique, "Duplicate case id in quote-cases.json.");
        Assert.That(ids, Does.Contain("truncating_labor_and_vat"),
            "The truncation tripwire is missing. Someone forked or trimmed the oracle.");
    }

    /// <summary>
    /// Every row in the JSON must be a <c>[TestCase]</c> on <see cref="Matches_oracle_fixture"/>
    /// and vice versa. A fixture row without a test case runs nowhere; a test case
    /// without a row fails on <c>Single</c>. Adding a case to <c>emit_fixtures.cpp</c>
    /// means regenerating the JSON and adding one <c>[TestCase]</c> line here.
    /// </summary>
    [Test]
    [Description("No orphaned fixture rows and no orphaned [TestCase] ids.")]
    public void Every_fixture_row_has_a_test_case_and_vice_versa()
    {
        var fixtureIds = LoadCases().Select(c => c.Id).OrderBy(x => x, StringComparer.Ordinal).ToList();
        var testCaseIds = typeof(QuoteCasesTests)
            .GetMethod(nameof(Matches_oracle_fixture), BindingFlags.Public | BindingFlags.Instance)!
            .GetCustomAttributes<TestCaseAttribute>()
            .Select(a => (string)a.Arguments[0]!)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        Assert.That(testCaseIds, Is.EqualTo(fixtureIds),
            "fixtures/quote-cases.json ids and [TestCase] ids on Matches_oracle_fixture must match exactly.");
    }

    // Use [TestCase("id")] rather than [TestCaseSource]. Test Explorer often does
    // not discover TestCaseSource (or only shows the JSON ids after SetName, so
    // Matches_oracle_fixture never appears). Each id is a row in quote-cases.json;
    // Every_fixture_row_has_a_test_case_and_vice_versa keeps the two lists in sync.
    //
    //   small_job_skips_markup         — two nails; markup bps is ignored (qty sum < 3)
    //   volume_discount_then_markup    — ten nails get 5% off, then markup on materials only
    //   price_book_overrides_line_unit — NAIL-100 is in the in-memory book, so 99999 on the line is a lie
    //   cache_miss_uses_line_unit      — CUSTOM-X is not in the book; SCREW-50 is (450 øre, not 1)
    //   truncating_labor_and_vat       — 7 * 80000 / 60 = 9333, not 9333.33 rounded
    //   largest_net_that_fits_int32    — net 858,993: the last value whose * 2500 fits 32-bit
    //   empty_materials_labor_only     — material_count 0; labor prices, markup 0
    //   non_positive_qty_skipped_and_qty_sum_three_gets_markup — qty 0 and -5 add nothing; qty 3 is the threshold
    //   negative_markup_not_clamped    — markup_bps -1000 reduces the net
    //   zero_rate_labor_is_zero        — minutes 60, rate 0 → labor 0
    //   non_positive_minutes_labor_is_zero — minutes -30, rate 80000 → labor 0
    //   null_sku_uses_line_unit        — sku null skips the book; unit_ore is used
    //
    // Expected øre still come from the JSON, not from these comments.
    [TestCase("small_job_skips_markup")]
    [TestCase("volume_discount_then_markup")]
    [TestCase("price_book_overrides_line_unit")]
    [TestCase("cache_miss_uses_line_unit")]
    [TestCase("truncating_labor_and_vat")]
    [TestCase("largest_net_that_fits_int32")]
    [TestCase("empty_materials_labor_only")]
    [TestCase("non_positive_qty_skipped_and_qty_sum_three_gets_markup")]
    [TestCase("negative_markup_not_clamped")]
    [TestCase("zero_rate_labor_is_zero")]
    [TestCase("non_positive_minutes_labor_is_zero")]
    [TestCase("null_sku_uses_line_unit")]
    [Description("C# output must match fixtures/quote-cases.json exactly (integer øre).")]
    public void Matches_oracle_fixture(string caseId)
    {
        var fixture = LoadCases().Single(c => c.Id == caseId);
        var actual = Run(fixture.Input);

        // Assert.Multiple reports every mismatched field, not just the first.
        Assert.Multiple(() =>
        {
            Assert.That(actual.MaterialsOre, Is.EqualTo(fixture.Expected.MaterialsOre), $"{caseId} materials_ore");
            Assert.That(actual.LaborOre, Is.EqualTo(fixture.Expected.LaborOre), $"{caseId} labor_ore");
            Assert.That(actual.MarkupOre, Is.EqualTo(fixture.Expected.MarkupOre), $"{caseId} markup_ore");
            Assert.That(actual.VatOre, Is.EqualTo(fixture.Expected.VatOre), $"{caseId} vat_ore");
            Assert.That(actual.TotalOre, Is.EqualTo(fixture.Expected.TotalOre), $"{caseId} total_ore");
        });
    }

    /// <summary>
    /// Seam for the slice calculator. Feeds the fixture request through
    /// <c>QuoteCalculator</c> with the in-memory price book.
    /// </summary>
    private static CalculateQuoteResponse Run(CalculateQuoteRequest request)
    {
        var calculator = new QuoteCalculator(new InMemoryPriceBook());
        return calculator.Calculate(request);
    }

    /// <summary>
    /// Reads the copied oracle from the test output directory
    /// (<c>fixtures/quote-cases.json</c> is linked in the test .csproj) directly
    /// into the slice records. Shared with the HTTP endpoint tests.
    /// </summary>
    internal static IReadOnlyList<QuoteCase> LoadCases()
    {
        var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "fixtures", "quote-cases.json");
        Assert.That(File.Exists(path), Is.True, $"Missing oracle file: {path}");

        var cases = JsonSerializer.Deserialize<List<QuoteCase>>(File.ReadAllText(path), JsonOptions);
        Assert.That(cases, Is.Not.Null.And.Not.Empty);

        return cases!;
    }
}

/// <summary>
/// One oracle row, deserialized straight from <c>fixtures/quote-cases.json</c>:
/// <c>id</c>, <c>input</c> (a slice request), <c>expected</c> (a slice response).
/// </summary>
public sealed record QuoteCase(
    string Id,
    CalculateQuoteRequest Input,
    CalculateQuoteResponse Expected);
