using Smartcraft.Quotes.Features.Quotes.CalculateQuote;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCalculateQuote();

var app = builder.Build();
app.MapCalculateQuote();
app.Run();
