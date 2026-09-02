namespace Smartcraft.Quotes.Features.Quotes.CalculateQuote;

/// <summary>
/// Migrated quote_price rules. All money is integer øre; <c>/</c> truncates toward zero.
/// </summary>
public sealed class QuoteCalculator
{
    private readonly IPriceBook _prices;

    public QuoteCalculator(IPriceBook prices) => _prices = prices;

    public CalculateQuoteResponse Calculate(CalculateQuoteRequest request)
    {
        var materialsOre = 0;
        var qtySum = 0;

        foreach (var line in request.Materials)
        {
            if (line.Quantity > 0)
            {
                qtySum += line.Quantity;
            }

            materialsOre += LineMaterialsOre(line);
        }

        var laborOre = 0;
        if (request.Labor.Minutes > 0 && request.Labor.RateOrePerHour != 0)
        {
            laborOre = request.Labor.Minutes * request.Labor.RateOrePerHour / 60;
        }

        var markupBps = qtySum < 3 ? 0 : request.MarkupBps;
        var markupOre = materialsOre * markupBps / 10000;

        var net = materialsOre + laborOre + markupOre;
        var vatOre = net * 2500 / 10000;
        var totalOre = net + vatOre;

        return new CalculateQuoteResponse(materialsOre, laborOre, markupOre, vatOre, totalOre);
    }

    private int LineMaterialsOre(QuoteMaterialLine line)
    {
        if (line.Quantity <= 0)
        {
            return 0;
        }

        var unit = _prices.UnitOreFor(line.Sku) ?? line.UnitOre;
        var ore = unit * line.Quantity;
        if (line.Quantity >= 10)
        {
            ore = ore * 95 / 100;
        }

        return ore;
    }
}
