namespace AndyTours.Application.Planung;

/// <summary>Raised when a planning GPX file exceeds the fixed per-file size limit.</summary>
public sealed class PlanungsDateiZuGrossException : Exception
{
    public PlanungsDateiZuGrossException() : base("Die GPX-Datei überschreitet das zulässige Größenlimit.") { }
}
