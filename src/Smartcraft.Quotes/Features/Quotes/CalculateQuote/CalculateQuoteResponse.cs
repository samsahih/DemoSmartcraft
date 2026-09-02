namespace Smartcraft.Quotes.Features.Quotes.CalculateQuote;

public sealed record CalculateQuoteResponse(
    int MaterialsOre,
    int LaborOre,
    int MarkupOre,
    int VatOre,
    int TotalOre);
