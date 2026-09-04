using Smartcraft.Quotes.Features.Quotes.CalculateQuote;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCalculateQuote();

var app = builder.Build();
app.MapCalculateQuote();
app.Run();

// Lets the test project host the real app in-process via WebApplicationFactory<Program>.
public partial class Program;
