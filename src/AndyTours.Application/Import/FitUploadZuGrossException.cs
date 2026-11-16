namespace AndyTours.Application.Import;

/// <summary>Signals that a FIT upload exceeded the configured byte limit.</summary>
public sealed class FitUploadZuGrossException : Exception
{
    public FitUploadZuGrossException() : base("FIT-Datei überschreitet die zulässige Größe.") { }
}
