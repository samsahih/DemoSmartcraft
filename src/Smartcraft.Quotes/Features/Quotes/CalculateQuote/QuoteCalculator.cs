namespace Smartcraft.Quotes.Features.Quotes.CalculateQuote;

/// <summary>
/// Migrated quote_price rules. All money is integer øre; <c>/</c> truncates toward zero.
/// </summary>
/// <remarks>
/// The C++ does this arithmetic in 32-bit <c>int</c>, where overflow is undefined
/// behaviour. Past roughly 858,993 øre net (about 8,590 NOK) <c>net * 2500</c> no
/// longer fits, and the legacy binary emits whatever its compiler happened to do
/// (a negative VAT on the build we measured). Rather than reproduce an accident,
/// the port runs the same math in a <c>checked</c> context and throws
/// <see cref="OverflowException"/>; the endpoint turns that into HTTP 400.
/// Every input the C++ answers correctly still produces identical øre.
/// </remarks>
public sealed class QuoteCalculator
{
    private readonly IPriceBook _prices;

    public QuoteCalculator(IPriceBook prices) => _prices = prices;

    /// <exception cref="OverflowException">
    /// An intermediate amount exceeded the 32-bit øre range the legacy engine supports.
    /// </exception>
    public CalculateQuoteResponse Calculate(CalculateQuoteRequest request)
    {
        checked
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
    }

    private int LineMaterialsOre(QuoteMaterialLine line)
    {
        if (line.Quantity <= 0)
        {
            return 0;
        }

        // `checked` does not flow into called methods, so this block needs its own.
        checked
        {
            var unit = _prices.UnitOreFor(line.Sku) ?? line.UnitOre;
            var ore = unit * line.Quantity;
            if (line.Quantity >= 10)
            {
                ore = ore * 95 / 100;
            }

            return ore;
        }
    }
}
