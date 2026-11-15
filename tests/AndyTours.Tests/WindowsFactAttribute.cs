using Xunit;

namespace AndyTours.Tests;

/// <summary>
/// Kennzeichnet einen Test, der Windows-Dateisystemsemantik oder Windows-Pfade benötigt.
/// Außerhalb von Windows wird der Test übersprungen.
/// </summary>
public sealed class WindowsFactAttribute : FactAttribute
{
    /// <summary>Erstellt das Attribut und überspringt den Test außerhalb von Windows.</summary>
    public WindowsFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip = "Benötigt Windows-Pfad- oder Dateisystemsemantik.";
        }
    }
}
