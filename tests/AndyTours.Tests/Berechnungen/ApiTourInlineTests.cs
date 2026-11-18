using AndyTours.App.ViewModels;
using AndyTours.Application.Touren;
using AndyTours.Contracts.V1;

namespace AndyTours.Tests.Berechnungen;

public sealed class ApiTourInlineTests
{
    private static readonly long[] Kategorien = [9, 3, 9];
    private static readonly (long Id, string Name)[] Aktivitaeten = [(1, "Bike"), (2, "Walk")];
    private static readonly (long Id, string Name)[] Equipments = [(7, "Alt"), (8, "Rad")];
    private static readonly (long Id, string Name)[] MehrdeutigeAktivitaeten = [(1, "Bike"), (2, "Bike")];
    private static readonly (long Id, string Name)[] AusgemusterteEquipments = [(8, "Rad (ausgemustert)")];
    internal static TourAntwort Tour() => new(42, "edit-start-version", new TourWerte(
        FahrerId: 7, Datum: new DateOnly(2026, 10, 24), Startzeit: new TimeOnly(9, 30),
        Name: "Alt", FahrzeitSekunden: 108307, DistanzKm: 52.34567, Hoehenmeter: 900,
        MaxHoehe: 1800, StartHoehe: 500, MaxGeschwindigkeit: 42.25,
        HfDurchschnitt: 130, HfMax: 175, HfMin: 70, Trittfrequenz: 85, Temperatur: 18,
        LeistungWatt: 205.5, KoerpergewichtKg: 71.2, Erholungspuls: 65,
        Trainingsbereich: "GA1", Kommentar: "unverändert", AktivitaetId: 1,
        EquipmentId: 7, KategorieIds: [.. Kategorien]));

    [Fact]
    public void Snapshot_und_ausgegebene_Arrays_sind_defensiv_kopiert()
    {
        var tour = Tour();
        var draft = ApiTourInlineEntwurf.Beginnen(tour);
        tour.Werte.KategorieIds[0] = 999;
        draft.Original.KategorieIds[1] = 888;
        draft.Werte.KategorieIds[2] = 777;
        Assert.Equal(Kategorien, draft.Werte.KategorieIds);
        Assert.False(draft.HatAenderungen);
        var edit = draft.Aendere("Name", "  Neu  ");
        Assert.True(edit.Valid);
        Assert.Equal("Neu", edit.Draft.Werte.Name);
        Assert.Equal("Alt", draft.Werte.Name);
        Assert.Equal(42, edit.Draft.TourId);
        Assert.Equal("edit-start-version", edit.Draft.Version);
        var actual = edit.Draft.Werte;
        Assert.Equal(draft.Original with { Name = "Neu", KategorieIds = actual.KategorieIds }, actual);
    }

    [Theory]
    [InlineData("Distanz")]
    [InlineData("Hoehenmeter")]
    [InlineData("MaxKmh")]
    [InlineData("PulsDurchschnitt")]
    [InlineData("PulsMax")]
    [InlineData("Trittfrequenz")]
    [InlineData("Temperatur")]
    [InlineData("MaxHoehe")]
    public void Leere_Zahlen_sind_nullwerte(string field)
    {
        var edit = ApiTourInlineEntwurf.Beginnen(Tour()).Aendere(field, "   ");
        Assert.True(edit.Valid);
        Assert.True(edit.Draft.HatAenderungen);
        var werte = edit.Draft.Werte;
        var number = field switch
        {
            "Distanz" => werte.DistanzKm, "Hoehenmeter" => werte.Hoehenmeter,
            "MaxKmh" => werte.MaxGeschwindigkeit, "PulsDurchschnitt" => werte.HfDurchschnitt,
            "PulsMax" => werte.HfMax, "Trittfrequenz" => werte.Trittfrequenz,
            "Temperatur" => werte.Temperatur, "MaxHoehe" => werte.MaxHoehe,
            _ => throw new InvalidOperationException(field)
        };
        Assert.Equal(0d, number);
        Assert.Equal(Kategorien, werte.KategorieIds);
    }

    [Theory]
    [InlineData("Distanz", "NaN")]
    [InlineData("Distanz", "Infinity")]
    [InlineData("Distanz", "-1")]
    [InlineData("Hoehenmeter", "1.5")]
    [InlineData("Hoehenmeter", "2147483648")]
    [InlineData("Temperatur", "1.5")]
    [InlineData("MaxHoehe", "-1")]
    [InlineData("Fahrzeit", "3:60")]
    [InlineData("Fahrzeit", "3:5")]
    [InlineData("Datum", "31.02.2026")]
    [InlineData("Startzeit", "24:00")]
    [InlineData("Kategorien", "1")]
    [InlineData("DurchschnittKmh", "1")]
    [InlineData("Kommentar", "Neu")]
    public void Ungueltig_laesst_Snapshot_unveraendert(string field, string value)
    {
        var draft = ApiTourInlineEntwurf.Beginnen(Tour());
        var edit = draft.Aendere(field, value);
        Assert.False(edit.Valid);
        Assert.Same(draft, edit.Draft);
    }

    [Fact]
    public void Legacy_Formate_und_NoOp()
    {
        var draft = ApiTourInlineEntwurf.Beginnen(Tour());
        Assert.False(draft.Aendere("Name", " Alt ").Draft.HatAenderungen);
        Assert.False(draft.Aendere("Distanz", "52,34567").Draft.HatAenderungen);
        Assert.False(draft.Aendere("Datum", "24.10.26").Draft.HatAenderungen);
        Assert.Null(draft.Aendere("Startzeit", "").Draft.Werte.Startzeit);
        Assert.Equal(108307, draft.Aendere("Fahrzeit", "30:05:07").Draft.Werte.FahrzeitSekunden);
        Assert.Equal(108000, draft.Aendere("Fahrzeit", "30:00").Draft.Werte.FahrzeitSekunden);
        Assert.Equal(-3, draft.Aendere("Temperatur", "-3").Draft.Werte.Temperatur);
        Assert.Equal("GA2", draft.Aendere("Trainingsbereich", " GA2 ").Draft.Werte.Trainingsbereich);
    }

    [Theory]
    [InlineData("Datum", "25.10.26")]
    [InlineData("Startzeit", "10:15")]
    [InlineData("Name", " Neu ")]
    [InlineData("Aktivitaet", "Walk")]
    [InlineData("Equipment", "Rad")]
    [InlineData("Distanz", "55,5")]
    [InlineData("Hoehenmeter", "1000")]
    [InlineData("Fahrzeit", "30:00")]
    [InlineData("MaxKmh", "65.2")]
    [InlineData("PulsDurchschnitt", "131")]
    [InlineData("PulsMax", "172")]
    [InlineData("Trittfrequenz", "86")]
    [InlineData("Temperatur", "-3")]
    [InlineData("MaxHoehe", "1900")]
    [InlineData("Trainingsbereich", " GA2 ")]
    public void Alle_fuenfzehn_Inlinefelder_bewahren_alle_anderen_Transportwerte(string field, string text)
    {
        var draft = ApiTourInlineEntwurf.Beginnen(Tour());
        var edit = draft.Aendere(field, text, Aktivitaeten, Equipments);
        Assert.True(edit.Valid);
        Assert.True(edit.Draft.HatAenderungen);
        var property = field switch
        {
            "Aktivitaet" => "AktivitaetId", "Equipment" => "EquipmentId",
            "Distanz" => "DistanzKm", "Fahrzeit" => "FahrzeitSekunden", "MaxKmh" => "MaxGeschwindigkeit",
            "PulsDurchschnitt" => "HfDurchschnitt", "PulsMax" => "HfMax", _ => field
        };
        var before = System.Text.Json.JsonSerializer.SerializeToElement(draft.Original);
        var after = System.Text.Json.JsonSerializer.SerializeToElement(edit.Draft.Werte);
        foreach (var original in before.EnumerateObject())
            if (original.Name != property)
                Assert.Equal(original.Value.GetRawText(), after.GetProperty(original.Name).GetRawText());
        Assert.Equal(draft.TourId, edit.Draft.TourId);
        Assert.Equal(draft.Version, edit.Draft.Version);
    }

    [Fact]
    public void Lookupnamen_muessen_eindeutig_sein()
    {
        var draft = ApiTourInlineEntwurf.Beginnen(Tour());
        Assert.False(draft.Aendere("Aktivitaet", "Bike", MehrdeutigeAktivitaeten).Valid);
        Assert.Equal(2, draft.Aendere("Aktivitaet", "Walk", Aktivitaeten).Draft.Werte.AktivitaetId);
        Assert.Equal(8, draft.Aendere("Equipment", "Rad (ausgemustert)", equipment: AusgemusterteEquipments).Draft.Werte.EquipmentId);
        Assert.Null(draft.Aendere("Equipment", "(kein)").Draft.Werte.EquipmentId);
        Assert.Null(draft.Aendere("Equipment", "").Draft.Werte.EquipmentId);
    }

    [Fact]
    public async Task Save_sendet_genau_eine_CAS_ohne_Read_oder_Refresh()
    {
        var fake = new Speicher();
        var writer = new ApiTourInlineSpeicher(fake);
        var draft = ApiTourInlineEntwurf.Beginnen(Tour()).Aendere("Name", "Neu").Draft;
        Assert.Equal(ApiInlineSpeicherErgebnis.Gespeichert, await writer.SpeichernAsync(draft));
        Assert.Equal(1, fake.Calls);
        Assert.Equal(42, fake.Id);
        Assert.Equal("edit-start-version", fake.Request!.Version);
        Assert.Equal("Neu", fake.Request.Werte.Name);
        Assert.Equal(Kategorien, fake.Request.Werte.KategorieIds);
    }

    [Fact]
    public async Task NoOp_und_vorheriger_Abbruch_senden_nichts()
    {
        var fake = new Speicher();
        var writer = new ApiTourInlineSpeicher(fake);
        var draft = ApiTourInlineEntwurf.Beginnen(Tour());
        Assert.Equal(ApiInlineSpeicherErgebnis.Unveraendert, await writer.SpeichernAsync(draft));
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        Assert.Equal(ApiInlineSpeicherErgebnis.Abgebrochen,
            await writer.SpeichernAsync(draft.Aendere("Name", "Neu").Draft, cancel.Token));
        Assert.Equal(0, fake.Calls);
    }

    [Theory]
    [InlineData(0, ApiInlineSpeicherErgebnis.Konflikt)]
    [InlineData(1, ApiInlineSpeicherErgebnis.NichtGefunden)]
    [InlineData(2, ApiInlineSpeicherErgebnis.Unklar)]
    public async Task Fehler_werden_nicht_wiederholt(int error, ApiInlineSpeicherErgebnis expected)
    {
        var fake = new Speicher { Error = error switch
        {
            0 => new TourVersionskonfliktException(),
            1 => new TourNichtGefundenException(42),
            _ => new HttpRequestException("unbekannter Sendestatus")
        }};
        var result = await new ApiTourInlineSpeicher(fake).SpeichernAsync(
            ApiTourInlineEntwurf.Beginnen(Tour()).Aendere("Name", "Neu").Draft);
        Assert.Equal(expected, result);
        Assert.Equal(1, fake.Calls);
    }

    [Fact]
    public async Task Laufender_Save_blockiert_zweiten_Send()
    {
        var gate = new TaskCompletionSource<TourAntwort>(TaskCreationOptions.RunContinuationsAsynchronously);
        var fake = new Speicher { Pending = gate.Task };
        var writer = new ApiTourInlineSpeicher(fake);
        var draft = ApiTourInlineEntwurf.Beginnen(Tour()).Aendere("Name", "Neu").Draft;
        var first = writer.SpeichernAsync(draft);
        Assert.Equal(ApiInlineSpeicherErgebnis.Beschaeftigt, await writer.SpeichernAsync(draft));
        Assert.Equal(1, fake.Calls);
        gate.SetResult(Tour());
        Assert.Equal(ApiInlineSpeicherErgebnis.Gespeichert, await first);
    }

    [Theory]
    [InlineData(500, true)]
    [InlineData(501, false)]
    public void Namensgrenze_wird_lokal_geprueft(int length, bool valid)
    {
        Assert.Equal(valid, ApiTourInlineEntwurf.Beginnen(Tour()).Aendere("Name", new string('N', length)).Valid);
    }

    [Theory]
    [InlineData(200, true)]
    [InlineData(201, false)]
    public void Bereichsgrenze_wird_lokal_geprueft(int length, bool valid)
    {
        Assert.Equal(valid, ApiTourInlineEntwurf.Beginnen(Tour()).Aendere("Trainingsbereich", new string('G', length)).Valid);
    }

    [Fact]
    public async Task Backend_400_ist_eindeutig_ungueltig_ohne_Retry()
    {
        var fake = new Speicher { Error = new UngueltigeTourException("Eingabe abgelehnt") };
        var writer = new ApiTourInlineSpeicher(fake);
        var draft = ApiTourInlineEntwurf.Beginnen(Tour()).Aendere("Name", "Neu").Draft;
        Assert.Equal(ApiInlineSpeicherErgebnis.Ungueltig, await writer.SpeichernAsync(draft));
        Assert.Equal(1, fake.Calls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task Unvollstaendige_Saveantwort_ist_unklar_ohne_Retry(int kind)
    {
        var fake = new Speicher { Antwort = () => kind switch
        {
            0 => null!,
            1 => Tour() with { Id = 99 },
            2 => Tour() with { Version = null! },
            3 => Tour() with { Version = "" },
            4 => Tour() with { Werte = null! },
            _ => Tour() with { Werte = Tour().Werte with { KategorieIds = null! } }
        }};
        var result = await new ApiTourInlineSpeicher(fake).SpeichernAsync(
            ApiTourInlineEntwurf.Beginnen(Tour()).Aendere("Name", "Neu").Draft);
        Assert.Equal(ApiInlineSpeicherErgebnis.Unklar, result);
        Assert.Equal(1, fake.Calls);
    }

    [Fact]
    public async Task Versionskennung_bleibt_opaque()
    {
        var fake = new Speicher { Antwort = () => Tour() with { Version = "opaque-new-version/not-a-hash" } };
        Assert.Equal(ApiInlineSpeicherErgebnis.Gespeichert, await new ApiTourInlineSpeicher(fake).SpeichernAsync(
            ApiTourInlineEntwurf.Beginnen(Tour()).Aendere("Name", "Neu").Draft));
        Assert.Equal("edit-start-version", fake.Request!.Version);
    }

    [Fact]
    public async Task Sendguard_wird_auch_nach_Transportabbruch_im_Finally_freigegeben()
    {
        var fake = new Speicher { Error = new OperationCanceledException("Sendestatus unbekannt") };
        var writer = new ApiTourInlineSpeicher(fake);
        var draft = ApiTourInlineEntwurf.Beginnen(Tour()).Aendere("Name", "Neu").Draft;
        Assert.Equal(ApiInlineSpeicherErgebnis.Unklar, await writer.SpeichernAsync(draft));
        Assert.Equal(1, fake.Calls);
        // A distinct, explicitly initiated call proves finally cleanup; the writer itself never retries.
        fake.Error = null;
        Assert.Equal(ApiInlineSpeicherErgebnis.Gespeichert, await writer.SpeichernAsync(
            ApiTourInlineEntwurf.Beginnen(Tour()).Aendere("Temperatur", "-3").Draft));
        Assert.Equal(2, fake.Calls);
    }

    private sealed class Speicher : ITourenSpeicher
    {
        public int Calls;
        public long Id;
        public TourAendernAnfrage? Request;
        public Exception? Error;
        public Func<TourAntwort>? Antwort;
        public Task<TourAntwort>? Pending;
        public Task<TourAntwort> Aendern(long id, TourAendernAnfrage anfrage, CancellationToken ct = default)
        {
            Calls++; Id = id; Request = anfrage;
            return Error is not null ? Task.FromException<TourAntwort>(Error) : Pending ?? Task.FromResult(Antwort is null ? Tour() : Antwort());
        }
        public Task<TourListeAntwort> Liste(int start, int anzahl, DateOnly? von, DateOnly? bis, CancellationToken ct = default) => throw new InvalidOperationException("Kein Reload während Save");
        public Task<TourListeAntwort> ListeGefiltert(int start, int anzahl, TourenListenfilter filter, CancellationToken ct = default) => throw new InvalidOperationException("Kein Reload während Save");
        public Task<TourAntwort?> Lesen(long id, CancellationToken ct = default) => throw new InvalidOperationException("Editstart-Version beibehalten");
        public Task<TourAntwort> Anlegen(TourWerte werte, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<TourAntwort> AnlegenIdempotent(TourWerte werte, Guid anfrageId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Loeschen(long id, string version, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
