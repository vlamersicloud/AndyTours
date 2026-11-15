using Dynastream.Fit;

namespace AndyTours.Import.Garmin;

/// <summary>
/// Prüft, ob eine geöffnete Datei eine unbeschädigte FIT-Datei ist.
/// </summary>
public static class PruefeFitDatei
{
    /// <summary>
    /// Prüft Kennung und Prüfsumme der Datei und spult sie danach an den Anfang zurück, damit der Decoder sie
    /// lesen kann.
    /// </summary>
    /// <param name="datei">Die geöffnete Datei.</param>
    /// <param name="decoder">Der Decoder des Garmin-FIT-SDK.</param>
    /// <returns><c>true</c>, wenn die Datei gelesen werden kann.</returns>
    public static bool Ausfuehren(Stream datei, Decode decoder)
    {
        bool istFit = decoder.IsFIT(datei);
        datei.Position = 0;
        bool gueltig = istFit && decoder.CheckIntegrity(datei);
        datei.Position = 0;
        return gueltig;
    }
}
