namespace AndyTours.App.ViewModels;

public static class ApiTourenMehrfachRueckmeldung
{
    public static string Text(ApiTourenMehrfachStatus status, bool frisch, string? ladeHinweis = null)
    {
        var outcome = status switch
        {
            ApiTourenMehrfachStatus.Bestaetigt when frisch => "Mehrfachaktion bestätigt; Übersicht aktualisiert.",
            ApiTourenMehrfachStatus.Bestaetigt => "Aktion bestätigt; die Übersicht konnte nicht aktualisiert werden.",
            ApiTourenMehrfachStatus.Abgelehnt => "Aktion abgelehnt. Bitte Auswahl neu laden und prüfen.",
            ApiTourenMehrfachStatus.Bereit => "Aktion nicht ausgeführt.",
            ApiTourenMehrfachStatus.Speichert => "Aktion läuft noch; Ergebnis ist nicht bestätigt.",
            ApiTourenMehrfachStatus.Unklar => "Aktionsergebnis ungewiss. Touren prüfen; nicht mit neuer Anfrage wiederholen.",
            _ => "Aktionsergebnis ungewiss. Touren prüfen; nicht mit neuer Anfrage wiederholen."
        };
        return string.IsNullOrWhiteSpace(ladeHinweis) ? outcome : $"{outcome} {ladeHinweis}";
    }
}
