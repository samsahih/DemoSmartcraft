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
    [Description("Locks the oracle case ids, including truncating_labor_and_vat.")]
    public void Fixture_file_is_the_oracle_and_includes_the_truncation_tripwire()
    {
        var cases = LoadCases();
        Assert.That(cases.Select(c => c.Id), Is.EqualTo(new[]
        {
            "small_job_skips_markup",         // qty sum < 3 → markup forced to 0
            "volume_discount_then_markup",    // line qty >= 10 → 5% off before markup
            "price_book_overrides_line_unit", // known SKU ignores the line's unit_ore
            "cache_miss_uses_line_unit",      // unknown SKU uses the line's unit_ore
            "truncating_labor_and_vat",       // 7 min labor and 25% VAT truncate toward 0
            "largest_net_that_fits_int32",    // 858,993 * 2500 is the last product that fits int
        }));
    }

    // Use [TestCase("id")] rather than [TestCaseSource]. Test Explorer often does
    // not discover TestCaseSource (or only shows the JSON ids after SetName, so
    // Matches_oracle_fixture never appears). Each id is a row in quote-cases.json:
    //
    //   small_job_skips_markup         — two nails; markup bps is ignored (qty sum < 3)
    //   volume_discount_then_markup    — ten nails get 5% off, then markup on materials only
    //   price_book_overrides_line_unit — NAIL-100 is in the in-memory book, so 99999 on the line is a lie
    //   cache_miss_uses_line_unit      — CUSTOM-X is not in the book; SCREW-50 is (450 øre, not 1)
    //   truncating_labor_and_vat       — 7 * 80000 / 60 = 9333, not 9333.33 rounded
    //   largest_net_that_fits_int32    — net 858,993: the last value whose * 2500 fits 32-bit
    //
    // Expected øre still come from the JSON, not from these comments.
    [TestCase("small_job_skips_markup")]
    [TestCase("volume_discount_then_markup")]
    [TestCase("price_book_overrides_line_unit")]
    [TestCase("cache_miss_uses_line_unit")]
    [TestCase("truncating_labor_and_vat")]
    [TestCase("largest_net_that_fits_int32")]
    [Description("C# output must match fixtures/quote-cases.json exactly (integer øre).")]
    public void Matches_oracle_fixture(string caseId)
    {
        var fixture = LoadCases().Single(c => c.Id == caseId);
        var actual = Run(fixture.Request);

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
    /// (<c>fixtures/quote-cases.json</c> is linked in the test .csproj).
    /// Maps JSON onto the existing slice records; it does not define a second DTO tree.
    /// </summary>
    internal static IReadOnlyList<QuoteCaseFixture> LoadCases()
    {
        var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "fixtures", "quote-cases.json");
        Assert.That(File.Exists(path), Is.True, $"Missing oracle file: {path}");

        var dtos = JsonSerializer.Deserialize<List<QuoteCaseDto>>(File.ReadAllText(path), JsonOptions);
        Assert.That(dtos, Is.Not.Null.And.Not.Empty);

        return dtos!.Select(d => d.ToFixture()).ToList();
    }

    // Private JSON shapes for System.Text.Json only. Production types stay in the slice.
    private sealed record QuoteCaseDto(string Id, QuoteCaseInputDto Input, QuoteCaseExpectedDto Expected)
    {
        public QuoteCaseFixture ToFixture()
        {
            var request = new CalculateQuoteRequest(
                Input.Materials.Select(m => new QuoteMaterialLine(m.Sku, m.Quantity, m.UnitOre)).ToList(),
                new QuoteLaborLine(Input.Labor.Minutes, Input.Labor.RateOrePerHour),
                Input.MarkupBps);

            var expected = new CalculateQuoteResponse(
                Expected.MaterialsOre,
                Expected.LaborOre,
                Expected.MarkupOre,
                Expected.VatOre,
                Expected.TotalOre);

            return new QuoteCaseFixture(Id, request, expected);
        }
    }

    private sealed record QuoteCaseInputDto(
        int MarkupBps,
        QuoteCaseLaborDto Labor,
        IReadOnlyList<QuoteCaseMaterialDto> Materials);

    private sealed record QuoteCaseLaborDto(int Minutes, int RateOrePerHour);

    private sealed record QuoteCaseMaterialDto(string Sku, int Quantity, int UnitOre);

    private sealed record QuoteCaseExpectedDto(
        int MaterialsOre,
        int LaborOre,
        int MarkupOre,
        int VatOre,
        int TotalOre);
}

/// <summary>
/// One oracle row: JSON id, slice request, slice expected response (all øre as <c>int</c>).
/// </summary>
public sealed record QuoteCaseFixture(
    string Id,
    CalculateQuoteRequest Request,
    CalculateQuoteResponse Expected);
