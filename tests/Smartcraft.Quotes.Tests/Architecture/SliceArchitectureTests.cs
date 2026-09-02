using System.Reflection;
using Smartcraft.Quotes.Features.Quotes.CalculateQuote;

namespace Smartcraft.Quotes.Tests.Architecture;

// These tests do not check quote prices. QuoteCasesTests does that against
// fixtures/quote-cases.json.
//
// They lock the *shape* of the port so a first-time reader (or agent) does not:
//   - invent a second DTO tree (Contracts / Domain / Application projects)
//   - change int øre to decimal and break truncating_labor_and_vat
//   - add EF/SQL even though the C++ price book is a static in-memory map
//
// If one of these fails, fix the design. Do not delete the test to go green.

public sealed class SliceArchitectureTests
{
    // Records already exist in Features/Quotes/CalculateQuote/. The namespace
    // below is that folder. A type in Smartcraft.Quotes.Contracts (or similar)
    // is a duplicate layer — the failure this test is here to catch.
    [Test]
    [Description("CalculateQuote records must stay in the feature slice namespace.")]
    public void Request_and_response_live_in_the_calculate_quote_slice()
    {
        const string slice = "Smartcraft.Quotes.Features.Quotes.CalculateQuote";

        Assert.That(typeof(CalculateQuoteRequest).Namespace, Is.EqualTo(slice),
            "CalculateQuoteRequest belongs in the slice, not a Contracts project.");
        Assert.That(typeof(CalculateQuoteResponse).Namespace, Is.EqualTo(slice),
            "CalculateQuoteResponse belongs in the slice, not a Contracts project.");
        Assert.That(typeof(QuoteMaterialLine).Namespace, Is.EqualTo(slice));
        Assert.That(typeof(QuoteLaborLine).Namespace, Is.EqualTo(slice));
    }

    // C++ money is int øre. Every / truncates toward zero (not banker's rounding).
    // decimal / double / float / long on these records would change
    // truncating_labor_and_vat without a compile error.
    // Nested types (QuoteLaborLine, the materials list) are not money; skip them.
    [Test]
    [Description("Slice money fields must stay int øre, not decimal/double/long.")]
    public void Quote_money_is_integer_ore_not_decimal()
    {
        foreach (var type in new[]
                 {
                     typeof(CalculateQuoteRequest),
                     typeof(CalculateQuoteResponse),
                     typeof(QuoteMaterialLine),
                     typeof(QuoteLaborLine),
                 })
        {
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var propertyType = property.PropertyType;

                // Nested records and lists are structure, not a currency unit.
                if (propertyType == typeof(decimal) ||
                    propertyType == typeof(double) ||
                    propertyType == typeof(float) ||
                    propertyType == typeof(long))
                {
                    Assert.Fail($"{type.Name}.{property.Name} must be int øre, not {propertyType.Name}.");
                }
            }
        }
    }

    // Vertical slice: one Quotes project + this test project.
    // Handler, calculator, and endpoint go in the same CalculateQuote folder
    // as the records. A new Domain/Application/Infrastructure/Contracts csproj
    // is Clean Architecture, which this repo is not using.
    [Test]
    [Description("No Contracts/Domain/Application/Infrastructure projects.")]
    public void Solution_has_no_layer_or_contracts_projects()
    {
        var names = Directory.GetFiles(FindRepoRoot(), "*.csproj", SearchOption.AllDirectories)
            .Select(Path.GetFileNameWithoutExtension)
            .ToList();

        Assert.That(names, Does.Not.Contain("Smartcraft.Quotes.Contracts"),
            "Do not add a Contracts project; slice records are the only DTO home.");
        Assert.That(names, Does.Not.Contain("Smartcraft.Quotes.Domain"));
        Assert.That(names, Does.Not.Contain("Smartcraft.Quotes.Application"));
        Assert.That(names, Does.Not.Contain("Smartcraft.Quotes.Infrastructure"));
    }

    // IPriceBook is the C++ static SKU → øre map, kept in process memory.
    // There is no database. Entity Framework, SqlClient, or Testcontainers
    // in Smartcraft.Quotes.csproj means the port invented a store the C++ never had.
    [Test]
    [Description("Quotes csproj must not pull in EF, SQL, or Testcontainers.")]
    public void Quotes_project_does_not_reference_ef_sql_or_testcontainers()
    {
        var csproj = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "Smartcraft.Quotes", "Smartcraft.Quotes.csproj"));

        Assert.That(csproj, Does.Not.Contain("EntityFramework").IgnoreCase,
            "No EF Core. Price book is in-memory.");
        Assert.That(csproj, Does.Not.Contain("SqlClient").IgnoreCase,
            "No SQL. There is no database.");
        Assert.That(csproj, Does.Not.Contain("Testcontainers").IgnoreCase,
            "No containerized database for this leaf.");
        Assert.That(csproj, Does.Not.Contain("DbContext"),
            "No DbContext. Use IPriceBook in the slice.");
    }

    // NUnit sets TestDirectory to bin/Debug/net10.0. Walk toward the repo root
    // so the csproj/layer checks work whether you run from IDE or `dotnet test`.
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "DemoSmartcraft.slnx")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not find DemoSmartcraft.slnx from the test directory.");
    }
}
