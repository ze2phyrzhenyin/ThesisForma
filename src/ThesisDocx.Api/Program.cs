var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/", () => Results.Json(new
{
    name = "ThesisDocx.Api",
    stage = "placeholder",
    message = "Stage 1 focuses on the core library, CLI, validation, and tests."
}));

app.MapPost("/render", () =>
{
    return Results.Problem("The web API surface is reserved for Stage 2. Use ThesisDocx.Cli in Stage 1.");
})
.WithName("RenderPlaceholder");

app.Run();
