# Dataset-Koordination: Prototyp und offene Integration

Ein kleiner In-Process-Leaseprototyp ist jetzt implementiert: `IDatenbestandskoordination` im Application-Koordination-Namespace sowie `Datenbestandskoordination` mit `ConcurrencyLimiter`, expliziter Kapazität/Warteschlange, normalen Ein-Permit-Leases, exklusiven All-Permit-Leases, `OldestFirst`, Cancellation und idempotenter Freigabe. Konstruktorwerte sind explizit; es gibt keine versteckten Kapazitätsdefaults. Prepared-Tests wurden kompiliert, nicht ausgeführt.

Der Prototyp ist **noch nicht an HTTP-Routen oder `GarminImportDienst`/`GarminImportPaketWorker` angeschlossen**. Deshalb schützt er derzeit keine tatsächlichen API-Lese-/Schreibzugriffe und erzeugt keinen kohärenten Backup-Snapshot. Integration muss an äußeren Eintrittspunkten erfolgen; verschachtelte Lease-Anforderung in aufgerufenen Services wäre deadlockgefährdet. Wartungszugang muss exklusiv sein und normale Leser/Schreiber drainieren.

Die Steuerung wirkt nur innerhalb dieses Prozesses. Sie schützt nicht vor zweiter Instanz, älteren Programmversionen oder externen SQLite-Schreibern und autorisiert keinen Live-Restore. Vor produktivem Dateiaustausch bleiben Deployment-Exklusivität, Prozess-/Instanzschutz, dauerhafter Recovery-Journal, vorherige Sicherung sowie Restart-/Rollback-Strategie erforderlich.

Nächster Schritt ist separat reviewed Integration mit expliziter Konfiguration, Sättigungsantwort und Worker-Einstieg; erst danach kann über Backup-/Restore-Routen entschieden werden. Kein produktiver Koordinator, Backup-Writer oder Restore ist derzeit wired.
