namespace Smartcraft.Quotes.Features.Quotes.CalculateQuote;

public static class CalculateQuoteEndpoint
{
    public static IServiceCollection AddCalculateQuote(this IServiceCollection services)
    {
        services.AddSingleton<IPriceBook, InMemoryPriceBook>();
        services.AddSingleton<QuoteCalculator>();
        services.AddSingleton<ICalculateQuoteHandler, CalculateQuoteHandler>();
        return services;
    }

    public static IEndpointRouteBuilder MapCalculateQuote(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/quotes/calculate", (CalculateQuoteRequest request, ICalculateQuoteHandler handler) =>
            Results.Ok(handler.Calculate(request)));
        return endpoints;
    }
}
