namespace Smartcraft.Quotes.Features.Quotes.CalculateQuote;

public interface IPriceBook
{
    /// <returns>The book price, or null when <paramref name="sku"/> is null or not in the book.</returns>
    int? UnitOreFor(string? sku);
}

/// <summary>
/// Process-lifetime SKU → øre map. Not a database.
/// </summary>
public sealed class InMemoryPriceBook : IPriceBook
{
    private static readonly Dictionary<string, int> Book = new(StringComparer.Ordinal)
    {
        ["NAIL-100"] = 1250,
        ["TIMB-2x4"] = 8900,
        ["SCREW-50"] = 450,
    };

    public int? UnitOreFor(string? sku)
    {
        if (string.IsNullOrEmpty(sku))
        {
            return null;
        }

        return Book.TryGetValue(sku, out var unitOre) ? unitOre : null;
    }
}
