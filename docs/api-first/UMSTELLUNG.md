# API-First Umstellung

## Status

Die API-first Architektur ist weiterhin eine schrittweise Migration, kein vollständiger Ersatz des Legacy-Clients. Der normale WPF-Start ohne `--api` bleibt bestehen. Im API-Modus laufen Backend-Fachzugriffe über typisierte Clients; UI-Assemblies sollen keine SQLite- oder Datenprojekt-Referenz erhalten.

Garmin Connect bietet im API-Modus inzwischen einen integrierten Download-/Review-/Übernahmeablauf: persistente 20er-Jobs, Worker-Resume, ZIP-Cache, serverseitige FIT-Prüfung, Kandidatenauswahl/Equipment und transaktionale Tour-/Garmin-Zuordnung. Das WPF-Jobfenster öffnet die synchrone Review-Auswahl; unmarkierte Kandidaten werden übersprungen und der Serverauftrag läuft unabhängig vom Dialog weiter. Diese Scheibe ist implementiert, aber nicht als vollständige Produktmigration oder E2E-Lauf verifiziert.

## Grenzen

USB/MTP-Geräteadapter und FIT-Upload, vollständige Tourdetails sowie Puls-/Planungsintegration und umfassende Backup-/Restore-/Crash-Recovery-Abdeckung bleiben offen. Legacy bleibt Standard und darf nicht als bereits entfernt beschrieben werden. Garmin-Key liegt im Backend in SQLite und dessen Sicherungen; er wird nicht an den Client zurückgegeben.

## Verifikation

API-, Client-, WPF- und Testprojekt-Release-Builds wurden kompiliert. Tests wurden nicht ausgeführt; keine echten Garmin-/API-Aufrufe und kein UI-Durchklicktest. Builds belegen Compilebarkeit, nicht vollständige Nutzerablauf-Verifikation.
