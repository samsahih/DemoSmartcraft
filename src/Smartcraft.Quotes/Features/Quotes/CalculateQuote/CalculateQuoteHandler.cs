namespace Smartcraft.Quotes.Features.Quotes.CalculateQuote;

public interface ICalculateQuoteHandler
{
    CalculateQuoteResponse Calculate(CalculateQuoteRequest request);
}

public sealed class CalculateQuoteHandler : ICalculateQuoteHandler
{
    private readonly QuoteCalculator _calculator;

    public CalculateQuoteHandler(QuoteCalculator calculator) => _calculator = calculator;

    public CalculateQuoteResponse Calculate(CalculateQuoteRequest request) =>
        _calculator.Calculate(request);
}
