namespace AndyTours.Application.Stammdaten;

/// <summary>Art eines fachlichen Stammdatenfehlers.</summary>
public enum StammdatenFehlerart
{
    /// <summary>Die Anfrage ist fachlich ungültig.</summary>
    Ungueltig,

    /// <summary>Der angeforderte Datensatz wurde nicht gefunden.</summary>
    NichtGefunden,

    /// <summary>Der Stammdatenbestand wurde zwischenzeitlich geändert.</summary>
    Konflikt
}

/// <summary>Fachlicher Fehler ohne technische oder vertrauliche Implementierungsdetails.</summary>
public sealed class StammdatenException : Exception
{
    /// <summary>Erzeugt einen fachlichen Stammdatenfehler mit sicherer Meldung.</summary>
    public StammdatenException(StammdatenFehlerart art, string message) : base(message)
    {
        Art = art;
    }

    /// <summary>Fachliche Fehlerkategorie.</summary>
    public StammdatenFehlerart Art { get; }
}
