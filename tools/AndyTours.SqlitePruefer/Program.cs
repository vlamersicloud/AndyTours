using System.Text.Json;
using AndyTours.Daten.Datensicherung;
using AndyTours.SqlitePruefer;

if (args.Length != 1 || !Path.IsPathFullyQualified(args[0]))
{
    Console.WriteLine(JsonSerializer.Serialize(SqlitePruefprotokoll.Ergebnis("invalid_input")));
    return 2;
}

var result = SqliteReadonlyPruefer.Pruefen(args[0]);
Console.WriteLine(JsonSerializer.Serialize(result));
return result.Status == "ok" ? 0 : 1;
