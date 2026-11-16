using System;
using System.Globalization;

namespace AndyTours.App.ViewModels;

/// <summary>Formatiert Tourendauern wie die Legacy-Anzeige ohne Stundenlimit.</summary>
public static class ApiTourDauerText
{
    public static string Formatiere(long? seconds)
    {
        if (seconds is null)
            return string.Empty;
        if (seconds.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(seconds), "Dauer darf nicht negativ sein.");

        long value = seconds.Value;
        long hours = value / 3600;
        long minutes = value / 60 % 60;
        long remainder = value % 60;
        return string.Create(CultureInfo.InvariantCulture, $"{hours}:{minutes:00}:{remainder:00}");
    }
}