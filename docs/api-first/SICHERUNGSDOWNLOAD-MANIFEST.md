# Sicherungsdownload – Quellpaketmanifest

Änderungen für einen ausdrücklich gestarteten Download einer bereits ausgewählten Katalogsicherung. Quellen gemeinsam geprüft und auf dem Testserver vollständig kompiliert; keine Tests, Appstarts oder Laufzeitprüfungen ausgeführt.

## Neue Dateien

- `src/AndyTours.Contracts/V1/SicherungsdownloadVertrag.cs`
- `src/AndyTours.Application/Sicherung/ISicherungsdownload.cs`
- `src/AndyTours.Application/Sicherung/ISicherungsdownloadClient.cs`
- `src/AndyTours.Api/SicherungsdownloadEndpunkt.cs`
- `src/AndyTours.Api/SicherungsdownloadGrenzen.cs`
- `src/AndyTours.Client/SicherungsdownloadHttpClient.cs`
- `src/AndyTours.App/ViewModels/ApiWiederherstellungsvorschauViewModel.Download.cs`
- `src/AndyTours.App/Funktionen/SicherungsdownloadDatei.cs`
- `tests/AndyTours.Tests/Sicherung/ApiSicherungsdownloadClientTests.cs`
- `tests/AndyTours.Tests/Sicherung/ApiSicherungsdownloadGrenzenTests.cs`
- `tests/AndyTours.Tests/Sicherung/SicherungsdownloadDateiTests.cs`
- `tests/AndyTours.Tests/Sicherung/ApiSicherungsdownloadStrukturTests.cs`
- `tests/AndyTours.Tests/Sicherung/SicherungsdownloadQuelleTests.cs`
- `tests/AndyTours.Tests/Sicherung/ApiWiederherstellungsvorschauDownloadTests.cs`
- `tests/AndyTours.Tests/Sicherung/ApiSicherungsdownloadOwnedResultTests.cs`

## Geänderte Dateien

- `src/AndyTours.Api/Program.cs` – Download-Endpunkt registriert; bestehende Authentifizierung und Datenbestands-Middleware bleiben davor.
- `src/AndyTours.Api/SicherungskatalogEndpunkte.cs` – List-Endpunktbeschreibung grenzt die neue separate Downloadaktion ab.
- `src/AndyTours.App/App.Api.cs` – Downloadclient in das Vorschau-ViewModel injiziert.
- `src/AndyTours.Daten/DateisystemSicherungskatalog.cs` – Katalog implementiert den typed Download-Port.
- `src/AndyTours.Daten/DateisystemSicherungskatalog.Pruefquelle.cs` – adaptiert denselben verifizierten Read-only-Quellhandle in den Download-Port.
- `src/AndyTours.Client/BackendClientSitzung.cs` – typisierten Streamingclient registriert.
- `src/AndyTours.App/ViewModels/ApiWiederherstellungsvorschauViewModel.cs` – Downloadclient-Injektion, Auswahl-/Close-Abbruch und Busy-Interlock; benannter Clientfactory für VM-Fakefixtures ohne Änderung der bestehenden Konstruktorauflösung.
- `src/AndyTours.App/Views/ApiWiederherstellungsvorschauFenster.xaml` und `.xaml.cs` – expliziter Download-Button und lokaler SaveFileDialog.
- `tests/AndyTours.Tests/AndyTours.Tests.csproj` – eindeutige Quellressourcen und Pure-Helper-Testlinks einschließlich Download-Partial.
- `tests/AndyTours.Tests/Daten/DateisystemSicherungskatalogTests.cs` – read-only Quellenstream, ID-/Hashablehnung und Quellimmutability.
- `docs/api-first/VOLLARCHIV-KATALOG.md`, `WIEDERHERSTELLUNGSVORSCHAU.md`, `UMSTELLUNG.md` und dieses Manifest.

## Sicherheits- und Bedienungsvertrag

`POST /api/v1/sicherungen/download` akzeptiert ausschließlich die opake Katalog-ID und den erwarteten SHA-256. Keine Clientpfade, beliebigen Serverdateien, SQL, Restore-, Delete- oder Retention-Operationen. Legacy-SQLite und Vollarchiv werden über den bestehenden Resolver mit readonly-Dateihandle und dem bestehenden Source-Hash-/Längencheck auf derselben geöffneten Quelle geprüft. Die Prüfung erfolgt vor Response-Headern; anschließend wird exakt die katalogisierte Byteanzahl gestreamt. Ein eigenes `IResult` hält den Quelllease durch Header und Body und entsorgt ihn im `finally` auch bei Abbruch oder Schreibfehler. Die existierende globale Authentifizierung und Datenbestandslease umschließen die Responseausgabe. Backend-Datenordner und Sicherungsordner müssen vertrauenswürdige, gegen externe Schreiber geschützte Verzeichnisse sein; dies ist kein Schutz gegen hostile Dateisystemschreiber oder TOCTOU außerhalb der verwalteten Lease.

Pflichtgrenze ohne Default: `Backend:Sicherung:MaxDownloadBytes` als positive invariant dezimale Int64-Anzahl. Fehlt sie oder ist sie ungültig, bleibt nur der Downloadendpunkt mit festem 503 deaktiviert. Ein Katalogeintrag über der Grenze wird mit 413 abgelehnt, ohne Teilantwort. Serverseitige Quellen bleiben unverändert.

Der Client verwendet `ResponseHeadersRead`, prüft Content-Length, Byteanzahl und SHA-256 beim Streamen in einen vom UI gewählten lokalen Pfad. WPF schreibt zunächst mit `CreateNew` in eine eigene temporäre Datei im Zielordner (Unix: nur Benutzer-Lese-/Schreibrechte; Windows: geerbte ACL des Zielordners), liest diese zur unabhängigen Größen-/Hashprüfung zurück und verschiebt erst danach ohne Overwrite. Bereits vorhandene Zieldateien bleiben unverändert. Vor dem atomaren Verschieben wird nur die eigene temporäre Datei best effort entfernt; Cleanupfehler können eine `.partial`-Datei zurücklassen. Nach erfolgreichem atomarem Verschieben ist die Zieldatei veröffentlicht und wird bei Close nicht gelöscht. Das Zielverzeichnis muss vertrauenswürdig und gegen konkurrierende externe Schreiber geschützt sein; hostile-FS-/TOCTOU-Schutz wird nicht behauptet. Es werden keine kompletten Archive im RAM gehalten.

Der Download ist weder SQLite-Prüfung noch Restorefreigabe. API, Windows-App und Testprojekt wurden mit SDK 10.0.402 vollständig (--no-restore --no-incremental) mit jeweils 0 Warnungen und 0 Fehlern gebaut. Die vorbereiteten Tests wurden nicht ausgeführt. Laufzeit-/Windows-Rendering, Authentifizierungsintegration, Abbruch während Response-Schreibens und sämtliche vorbereiteten Tests bleiben ungeprüft. Restore/Apply, Löschung und Retention sind ausdrücklich nicht enthalten.

## Buildnachweis

Remote: 192.168.178.32, SDK 10.0.402, Release, EnableWindowsTargeting=true. Jeweils 0 Warnungen / 0 Fehler:
- API: `/tmp/andytours-backup-download-api-final-20261118.log`
- Windows-App: `/tmp/andytours-backup-download-app-final-20261118.log`
- Testprojekt (nur Compile): `/tmp/andytours-backup-download-tests-final-20261118.log`

Kein HTTP-Aufruf, Testlauf, echter Download-, Backup- oder Restorelauf.
