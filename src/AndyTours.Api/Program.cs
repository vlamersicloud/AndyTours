using AndyTours.Contracts.V1;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Versionierte Systemressource: bewusst keine beliebigen Datei- oder SQL-Parameter akzeptieren.
app.MapGet("/api/v1/health", () => Results.Ok(new VerbindungAntwort("bereit", "v1")))
    .WithName("BackendStatus")
    .WithTags("System")
    .Produces<VerbindungAntwort>();

app.Run();

/// <summary>Für Integrationstests zugänglicher Einstiegspunkt des API-Hosts.</summary>
public partial class Program;
