using AndyTours.App.ViewModels;

namespace AndyTours.Tests.Darstellung;

public sealed class ApiTourenMehrfachRueckmeldungTests
{
    [Theory]
    [InlineData(ApiTourenMehrfachStatus.Bestaetigt, true, "Mehrfachaktion bestätigt; Übersicht aktualisiert.")]
    [InlineData(ApiTourenMehrfachStatus.Bestaetigt, false, "Aktion bestätigt; die Übersicht konnte nicht aktualisiert werden.")]
    [InlineData(ApiTourenMehrfachStatus.Abgelehnt, true, "Aktion abgelehnt. Bitte Auswahl neu laden und prüfen.")]
    [InlineData(ApiTourenMehrfachStatus.Bereit, false, "Aktion nicht ausgeführt.")]
    [InlineData(ApiTourenMehrfachStatus.Unklar, false, "Aktionsergebnis ungewiss. Touren prüfen; nicht mit neuer Anfrage wiederholen.")]
    public void Outcome_text_distinguishes_confirmation_rejection_and_no_execution(
        ApiTourenMehrfachStatus status, bool fresh, string expected) =>
        Assert.Equal(expected, ApiTourenMehrfachRueckmeldung.Text(status, fresh));

    [Theory]
    [InlineData(ApiTourenMehrfachStatus.Bestaetigt, "Aktion bestätigt; die Übersicht konnte nicht aktualisiert werden.")]
    [InlineData(ApiTourenMehrfachStatus.Abgelehnt, "Aktion abgelehnt. Bitte Auswahl neu laden und prüfen.")]
    [InlineData(ApiTourenMehrfachStatus.Bereit, "Aktion nicht ausgeführt.")]
    [InlineData(ApiTourenMehrfachStatus.Unklar, "Aktionsergebnis ungewiss. Touren prüfen; nicht mit neuer Anfrage wiederholen.")]
    public void Outcome_and_reload_hint_are_both_preserved(ApiTourenMehrfachStatus status, string prefix)
    {
        var result = ApiTourenMehrfachRueckmeldung.Text(status, false, "Die zuvor gewählte Aktivität ist nicht mehr vorhanden.");
        Assert.StartsWith(prefix, result, StringComparison.Ordinal);
        Assert.Contains("Die zuvor gewählte Aktivität ist nicht mehr vorhanden.", result, StringComparison.Ordinal);
    }
}
