namespace Smartcraft.Quotes.Features.Quotes.CalculateQuote;

public sealed record QuoteMaterialLine(string Sku, int Quantity, int UnitOre);

public sealed record QuoteLaborLine(int Minutes, int RateOrePerHour);

public sealed record CalculateQuoteRequest(
    IReadOnlyList<QuoteMaterialLine> Materials,
    QuoteLaborLine Labor,
    int MarkupBps);
