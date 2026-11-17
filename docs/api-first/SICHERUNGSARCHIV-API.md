# Vollarchiv-Erstellungs-API: Designplan

- Authentifizierten `POST /api/v1/sicherungen/` ergänzen. Request enthält nur nichtleere Vorgangs-GUID; keine Pfade/Limits. Derselbe Schlüssel liefert dieselbe validierte unveränderliche Ausgabe.
- Endpoint mit `ExklusiverDatenbestand` markieren: Middleware übernimmt den einzigen äußeren exklusiven In-Process-Lease. Creator/Port erwirbt keinen Nested Lease; externe Prozesse werden dadurch nicht koordiniert.
- Creator mit festem `Backend:Datenordner`, readonly ermitteltem Backupordner, `TimeProvider` und expliziten `SicherungsarchivGrenzen` registrieren; vorhandene Ordner-/Counter-Policy nicht ändern.
- `SicherungsarchivKonfiguration.TryLesen` prüft sieben Pflichtwerte unter `Backend:Sicherung`, ohne Defaults. Der lazy `SicherungsarchivBereitstellung`-Baustein liest sie und die Backupordnereinstellung erst beim Aufruf; SQLite wird read-only/unpooled und ohne Migration geöffnet. Er ändert keine Einstellungen/Zähler, erstellt keine Ordner und verursacht keine Startup-I/O. Nicht verfügbare Konfiguration/Pfade liefern `null`.
- API-/DI-/POST-Wiring ist noch nicht vorhanden. Später soll fehlende/ungültige Konfiguration nur die neue Funktion mit festem 503-Problem deaktivieren, nicht Startup/Health oder andere Services. Limits müssen explizit vom Deployment gesetzt werden.
- Antwort enthält nur opaque Metadaten und Retry-Status. Legacy-Katalog listet neue Vollarchive weiterhin nicht; separater Katalogbedarf. Kein Download, Restore, Löschen oder Retention.
- Keine Produktionskonfiguration geändert. Prepared Tests sind nicht ausgeführt. Deployment muss externe Writer/TOCTOU ausschließen.
