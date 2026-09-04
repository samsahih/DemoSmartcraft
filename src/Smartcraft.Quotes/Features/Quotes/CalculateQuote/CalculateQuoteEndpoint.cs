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
        {
            try
            {
                return Results.Ok(handler.Calculate(request));
            }
            catch (OverflowException)
            {
                // The legacy engine's 32-bit math cannot represent this quote.
                // Refuse it rather than return a wrapped, wrong total with a 200.
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Quote too large to calculate",
                    detail: "An intermediate amount exceeded the 32-bit øre range supported by the pricing engine. Split the job into smaller quotes.");
            }
        });
        return endpoints;
    }
}
