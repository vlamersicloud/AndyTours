namespace AndyTours.Application.Tabellenlayout;

public sealed class EinheitentabellenlayoutNichtVerfuegbarException : Exception
{
    public EinheitentabellenlayoutNichtVerfuegbarException() : base("Tabellenlayout ist derzeit nicht verfügbar.") { }
}
