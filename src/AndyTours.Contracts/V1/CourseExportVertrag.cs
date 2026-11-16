namespace AndyTours.Contracts.V1;

/// <summary>Sportprofil einer serverseitig erzeugten FIT-Strecke.</summary>
public enum CourseFahrprofil { Rennrad, Trekking, Mountainbike }

/// <summary>Explizite FIT-Streckenexport-Voreinstellungen des Backends.</summary>
public sealed record CourseExportEinstellungenAntwort(CourseFahrprofil StandardProfil);

/// <summary>Benutzerbestätigte Messpunkte; enthält weder Dateipfade noch Netzwerkziele.</summary>
public sealed record CourseExportAnfrage(string Name, CourseFahrprofil Profil, IReadOnlyList<TourdetailMesspunkt> Verlauf);

/// <summary>Erzeugte FIT-Datei mit sicherem Dateinamen und optionalem endlichem Warncode.</summary>
public sealed record CourseExportAntwort(byte[] Inhalt, string Dateiname, string? Warncode);
