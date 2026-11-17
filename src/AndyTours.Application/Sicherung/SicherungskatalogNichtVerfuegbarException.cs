namespace AndyTours.Application.Sicherung;

/// <summary>
/// Signalisiert, dass der vollständige Sicherungskatalog für diese Anfrage nicht zuverlässig bereitgestellt werden kann.
/// Die API kann dies anfragebezogen mit HTTP 503 beantworten, ohne den Anwendungsstart zu verhindern.
/// Es werden weder Teilkataloge ausgegeben noch Dateien oder Einstellungen verändert.
/// </summary>
public sealed class SicherungskatalogNichtVerfuegbarException : Exception
{
    public SicherungskatalogNichtVerfuegbarException()
        : base("Vollarchiv-Katalog ist derzeit nicht verfügbar.")
    {
    }
}
