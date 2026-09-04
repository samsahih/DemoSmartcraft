namespace Smartcraft.Quotes.Features.Quotes.CalculateQuote;

/// <param name="Sku">
/// Nullable, like the C++ <c>const char*</c>. A null or unknown SKU means the price
/// book is skipped and <paramref name="UnitOre"/> is used. JSON <c>null</c> binds here.
/// </param>
public sealed record QuoteMaterialLine(string? Sku, int Quantity, int UnitOre);

public sealed record QuoteLaborLine(int Minutes, int RateOrePerHour);

public sealed record CalculateQuoteRequest(
    IReadOnlyList<QuoteMaterialLine> Materials,
    QuoteLaborLine Labor,
    int MarkupBps);
