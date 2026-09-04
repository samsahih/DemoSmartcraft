using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Smartcraft.Quotes.Features.Quotes.CalculateQuote;

namespace Smartcraft.Quotes.Tests.CalculateQuote;

/// <summary>
/// Drives the real HTTP endpoint in-process. <c>QuoteCasesTests</c> proves the
/// calculator matches the C++; this proves the web layer around it (JSON binding,
/// camelCase naming, routing, DI) does not change the numbers on the way through,
/// and that an oversized quote is refused instead of returned wrong.
/// </summary>
public sealed class CalculateQuoteEndpointTests
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public void StartHost()
    {
        _factory = new WebApplicationFactory<Program>();
        _client = _factory.CreateClient();
    }

    [OneTimeTearDown]
    public void StopHost()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    // Same ids as QuoteCasesTests. Expected øre still come from fixtures/quote-cases.json.
    [TestCase("small_job_skips_markup")]
    [TestCase("volume_discount_then_markup")]
    [TestCase("price_book_overrides_line_unit")]
    [TestCase("cache_miss_uses_line_unit")]
    [TestCase("truncating_labor_and_vat")]
    [TestCase("largest_net_that_fits_int32")]
    [Description("POST /quotes/calculate returns the oracle numbers for every fixture row.")]
    public async Task Fixture_case_round_trips_through_http(string caseId)
    {
        var fixture = QuoteCasesTests.LoadCases().Single(c => c.Id == caseId);

        var response = await _client.PostAsJsonAsync("/quotes/calculate", fixture.Request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), $"{caseId} status");
        var actual = await response.Content.ReadFromJsonAsync<CalculateQuoteResponse>();
        Assert.That(actual, Is.EqualTo(fixture.Expected), $"{caseId} body");
    }

    // 100 x TIMB-2x4 at 8,900 øre = 890,000; 5% volume discount -> 845,500 materials.
    // Plus 80,000 labor: net 925,500. net * 2500 = 2,313,750,000 > int.MaxValue.
    // The C++ wraps this to VAT -198,121 and total 727,379 (undefined behaviour).
    // The port refuses the input instead of returning that with a 200.
    [Test]
    [Description("A quote whose math overflows 32-bit øre returns 400, not a wrapped total.")]
    public async Task Oversized_quote_returns_400_instead_of_a_wrong_total()
    {
        var request = new CalculateQuoteRequest(
            [new QuoteMaterialLine("TIMB-2x4", 100, 0)],
            new QuoteLaborLine(60, 80000),
            0);

        var response = await _client.PostAsJsonAsync("/quotes/calculate", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.That(problem, Is.Not.Null);
        Assert.That(problem!.Title, Is.EqualTo("Quote too large to calculate"));
    }

    // One øre past the largest_net_that_fits_int32 fixture row. That row proves the
    // C++ still answers at 858,993; this proves the port refuses at 858,994 rather
    // than inventing a number the C++ never legally produced.
    [Test]
    [Description("One øre past the 32-bit ceiling is refused; the ceiling itself is a fixture row.")]
    public async Task One_ore_past_the_ceiling_is_refused()
    {
        var request = new CalculateQuoteRequest(
            [new QuoteMaterialLine("CUSTOM-X", 1, 858_994)],
            new QuoteLaborLine(0, 0),
            0);

        var response = await _client.PostAsJsonAsync("/quotes/calculate", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }
}
