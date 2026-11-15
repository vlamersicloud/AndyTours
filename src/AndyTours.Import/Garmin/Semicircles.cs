namespace AndyTours.Import.Garmin;

/// <summary>
/// Rechnet Koordinaten zwischen Grad und „Semicircles“ um, der Einheit des FIT-Formats.
/// </summary>
/// <remarks>2³¹ Semicircles entsprechen 180°; so passt jede Koordinate in eine ganze 32-Bit-Zahl.</remarks>
public static class Semicircles
{
    /// <summary>Semicircles je Grad.</summary>
    private const double JeGrad = 2147483648.0 / 180;

    /// <summary>Rechnet Semicircles in Grad um.</summary>
    /// <param name="semicircles">Wert aus der FIT-Datei.</param>
    /// <returns>Die Koordinate in Grad.</returns>
    public static double InGrad(int semicircles) => semicircles * (180.0 / 2147483648.0);

    /// <summary>Rechnet Grad in Semicircles um (gerundet).</summary>
    /// <param name="grad">Die Koordinate in Grad.</param>
    /// <returns>Der Wert für die FIT-Datei.</returns>
    public static int AusGrad(double grad) => (int)Math.Round(grad * JeGrad);
}
