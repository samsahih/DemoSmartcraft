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
            var errors = ShapeErrors(request);
            if (errors.Count > 0)
            {
                return Results.ValidationProblem(errors);
            }

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

    /// <summary>
    /// The slice records declare <c>Materials</c> and <c>Labor</c> non-nullable, but
    /// JSON can omit them or send <c>null</c> and System.Text.Json binds that anyway.
    /// This is the HTTP adapter's job: reject bodies that do not match the contract
    /// before they reach the calculator, which assumes the contract holds.
    /// An empty materials list is a legal legacy input and is not an error here.
    /// </summary>
    private static Dictionary<string, string[]> ShapeErrors(CalculateQuoteRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (request.Materials is null)
        {
            errors["materials"] = ["materials is required (use an empty array for a quote with no materials)."];
        }
        else
        {
            for (var i = 0; i < request.Materials.Count; i++)
            {
                if (request.Materials[i] is null)
                {
                    errors[$"materials[{i}]"] = ["material line must not be null."];
                }
            }
        }

        if (request.Labor is null)
        {
            errors["labor"] = ["labor is required (use minutes 0 and rateOrePerHour 0 for no labor)."];
        }

        return errors;
    }
}
