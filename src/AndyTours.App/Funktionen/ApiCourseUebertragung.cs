using System.Windows;
using AndyTours.Application.Export;
using AndyTours.Application.Stammdaten;
using AndyTours.App.ViewModels;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;

namespace AndyTours.App.Funktionen;

public static class ApiCourseUebertragung
{
    public static async Task Zeige(Window owner, IReadOnlyList<Coursezeile> zeilen, ICourseExport export, CancellationToken cancellationToken)
    {
        try
        {
            var settings = await export.LadenEinstellungen(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!owner.IsVisible) return;
            var profile = settings.StandardProfil switch
            {
                CourseFahrprofil.Rennrad => Fahrprofil.Rennrad,
                CourseFahrprofil.Trekking => Fahrprofil.Trekking,
                CourseFahrprofil.Mountainbike => Fahrprofil.Mountainbike,
                _ => throw new StammdatenException(StammdatenFehlerart.Ungueltig, "Course-Profil ungültig.")
            };
            if (zeilen.Any(row => row.Verlauf is null))
            {
                MessageBox.Show(owner, "Planungscourses werden in diesem Dialog noch nicht unterstützt.",
                    CourseUebertragenViewModel.Titel, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            CourseUebertragenViewModel.ZeigeApi(owner, zeilen, profile, async (row, selected, ct) =>
            {
                var contractProfile = selected switch
                {
                    Fahrprofil.Rennrad => CourseFahrprofil.Rennrad,
                    Fahrprofil.Trekking => CourseFahrprofil.Trekking,
                    Fahrprofil.Mountainbike => CourseFahrprofil.Mountainbike,
                    _ => throw new StammdatenException(StammdatenFehlerart.Ungueltig, "Course-Profil ungültig.")
                };
                var points = row.Verlauf!.Select(p => new TourdetailMesspunkt(
                    p.ZeitpunktUtc is DateTime utc ? new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)) : null,
                    p.DistanzKm,
                    p.Fahrzeit.Ticks / TimeSpan.TicksPerMillisecond,
                    p.Hoehe, p.Puls, p.Geschwindigkeit, p.Trittfrequenz, p.Temperatur,
                    p.Position is Geoposition pos ? new TourdetailPosition(pos.Breite, pos.Laenge) : null)).ToArray();
                return await export.Erstellen(new CourseExportAnfrage(row.Name, contractProfile, points), ct);
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception ex) when (ex is StammdatenException or Client.BackendApiException or System.Net.Http.HttpRequestException)
        {
            if (owner.IsVisible)
                MessageBox.Show(owner, "Course-Einstellungen oder Export sind derzeit nicht verfügbar.",
                    CourseUebertragenViewModel.Titel, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}