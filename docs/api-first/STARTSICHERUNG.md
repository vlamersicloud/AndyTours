# Backend-Startsicherung

## Verhalten des Quellpakets

Beim API-Prozessstart führt genau ein registrierter `IHostedLifecycleService` den Koordinator awaited in `StartingAsync` aus. ASP.NET Core ruft diese Lifecyclephase vor `IHostedService.StartAsync` aller Hosted Services auf; damit läuft die Sicherung vor Garmin-Worker und Listenerstart und erhält das echte Hoststart-CancellationToken. Es gibt keinen direkten zweiten Aufruf in `Program`. Es zählt Backendstarts, nicht Clientverbindungen. Der Ablauf hält eine einzelne exklusive In-Process-Datenbestandslease während Zähler, Fälligkeitsprüfung, Archivierung und Rücksetzen. Externe Datenbankwriter werden dadurch nicht koordiniert und müssen betrieblich ausgeschlossen werden.

Der Counter-Adapter öffnet nur eine vorhandene Datenbank mit SQLite `ReadWrite` und `Pooling=false`; er erstellt keine Datei, Tabelle oder Migration. Er erhöht ausschließlich `StartsSeitSicherung`, saturiert bei `int.MaxValue` und erhält die übrigen Einstellungswerte unverändert. Das Intervall verwendet die bestehende Sicherungsrichtlinie (zulässig 1–99; bei fehlendem Wert den bestehenden Model-Standard). Eine Sicherung wird nur bei `IstSicherungFaellig` und `Trainingseinheiten > 0` angefordert. Ein frischer opaker Vorgangsschlüssel wird pro Start erzeugt. Der Zähler wird per Compare-and-Swap nur nach erfolgreicher Veröffentlichung eines Vollarchivs auf null gesetzt. Die Antwort muss die Vorgangs-ID und Vollarchiv-Art erfüllen und wird zusätzlich mit der gemeinsamen `WiederherstellungsvorschauRegeln.IstKatalogeintragGueltig`-Validierung geprüft (Hash, positive Größe, UTC-Zeitstempel ohne Legacy-Zeitstempel). Ein bereits vorhandenes Archiv wird fail-closed abgelehnt; es gibt keinen automatischen Retry.

Fehlende Datenbank, ungültige Richtlinie, fehlende Archivkonfiguration oder Archivfehler verhindern nicht den normalen Hoststart; der Zähler bleibt fällig. Host-Cancellation wird weitergereicht, auch wenn ein Archivport sein Token ignoriert. Die SQLite- und Dateisystemoperationen enthalten synchrone Provider-/Dateisystemschritte; eine harte Abbruchdeadline kann dafür nicht garantiert werden. Logs enthalten nur den Ergebnisstatus, keine Datei- oder Fehlerdetails. Der Pfad ruft weder den Legacy-Starttrigger noch Retention/Löschung auf. Dies ist kein Restore-, Restore-Guard- oder Schutz vor externen Schreibern.

## Pflichtkonfiguration

Die bestehende Vollarchiv-Konfiguration muss vollständig und gültig sein; ohne sie ist nur die Startsicherung nicht verfügbar. Für die vorhandenen sieben Archivlimits gelten weiterhin die bereits dokumentierten Konfigurationsschlüssel und es werden keine Defaults ergänzt. Produktionswerte werden hier nicht festgelegt.

## Verifikation

Quellfixtures decken den Koordinator, Zähler-CAS und Struktur der Startverdrahtung ab. Eine echte Lifecycle-Startfixture prüft den awaited Hook und Host-Cancellation ohne einen Host zu starten. Sie wurden nicht ausgeführt. API, Windows-App und Testprojekt wurden auf 192.168.178.32 mit SDK 10.0.402 vollständig (Release, --no-restore --no-incremental, EnableWindowsTargeting=true) jeweils mit 0 Warnungen und 0 Fehlern kompiliert. Keine Tests, App-/Hoststarts, SQL-Fixtures oder echten Sicherungen wurden ausgeführt; Kompilierung ist keine Laufzeitabnahme.

Buildlogs: `/tmp/andytours-startup-backup-api-final-20261118.log`, `/tmp/andytours-startup-backup-app-final-20261118.log`, `/tmp/andytours-startup-backup-tests-final-20261118.log`.
