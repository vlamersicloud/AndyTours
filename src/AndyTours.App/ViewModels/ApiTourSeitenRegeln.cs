using System;
using System.Collections.Generic;
using System.Linq;
using AndyTours.Contracts.V1;

namespace AndyTours.App.ViewModels;

/// <summary>Feste Protokollausnahme für eine unvollständige oder inkonsistente Tourenseite.</summary>
public sealed class ApiTourSeitenException : Exception
{
    public ApiTourSeitenException()
        : base("Tourenseite ist unvollständig oder ungültig. Bitte Backend-Version und Verbindung prüfen.")
    {
    }
}

/// <summary>Validiert vollständig geladene Tourenseiten für die spätere 18-Spalten-Anzeige.</summary>
public static class ApiTourSeitenRegeln
{
    public static void Validieren(TourListeAntwort? page, int expectedStart, int expectedAnzahl)
    {
        if (page is null || expectedStart < 0 || expectedAnzahl <= 0 ||
            page.Start != expectedStart || page.Anzahl != expectedAnzahl ||
            page.Gesamt < 0 || page.Eintraege is null)
            throw new ApiTourSeitenException();

        long verbleibend = Math.Max(0L, (long)page.Gesamt - page.Start);
        long erwartet = Math.Min((long)page.Anzahl, verbleibend);
        if (page.Eintraege.Count != erwartet)
            throw new ApiTourSeitenException();

        var ids = new HashSet<long>();
        foreach (TourAntwort? tour in page.Eintraege)
        {
            if (tour is null || tour.Id <= 0 || !ids.Add(tour.Id) ||
                tour.Version is null || tour.Version.Length != 64 ||
                tour.Version.Any(c => !Uri.IsHexDigit(c)) ||
                tour.Werte is null || tour.Werte.KategorieIds is null)
                throw new ApiTourSeitenException();

            TourAnzeigeAntwort? a = tour.Anzeige;
            TourWerte w = tour.Werte;
            if (a is null || a.Name is null || a.Aktivitaet is null || a.Kategorien is null ||
                a.Equipment is null || a.Trainingsbereich is null || a.Kommentar is null ||
                (a.DistanzKm.HasValue && !double.IsFinite(a.DistanzKm.Value)) ||
                (a.DurchschnittKmh.HasValue && !double.IsFinite(a.DurchschnittKmh.Value)) ||
                (a.MaxKmh.HasValue && !double.IsFinite(a.MaxKmh.Value)) ||
                a.Datum != w.Datum || a.Startzeit != w.Startzeit || a.Name != w.Name ||
                a.Hoehenmeter != w.Hoehenmeter || a.Trainingsbereich != w.Trainingsbereich ||
                a.Kommentar != w.Kommentar)
                throw new ApiTourSeitenException();
        }
    }
}