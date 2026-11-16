namespace AndyTours.Application.Overlays;

/// <summary>Raised when a streamed GPX file exceeds the fixed per-file size limit.</summary>
public sealed class OverlayGpxZuGrossException : Exception
{
    public OverlayGpxZuGrossException()
        : base("Die GPX-Datei überschreitet das zulässige Größenlimit.")
    {
    }
}
