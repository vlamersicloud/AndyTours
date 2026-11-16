using AndyTours.Application.Planung;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Daten;
using AndyTours.App.ViewModels;

namespace AndyTours.Tests.Darstellung;

public sealed class ApiRoutingKonfigurationViewModelTests
{
    [Fact]
    public async Task SQLite_load_is_read_only_and_setters_do_not_persist()
    {
        var dir = Path.Combine(Path.GetTempPath(), "routing-vm-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var dbPath = Path.Combine(dir, "db.sqlite");
        try
        {
            using (var db = OeffneDatenbank.Ausfuehren(dbPath))
            {
                EinstellungTabelle.Schreiben(db, null, Einstellungsschluessel.RoutingBRouterAdresse, "https://router.example/path");
                EinstellungTabelle.Schreiben(db, null, Einstellungsschluessel.RoutingOpenrouteserviceSchluessel, "FAKE-KEY");
            }
            var vm = new ApiRoutingKonfigurationViewModel(new SqliteRoutingKonfiguration(dbPath));
            await vm.LadenCommand.ExecuteAsync(default);
            Assert.Equal("https://router.example/path", vm.GespeicherteAdresse);
            vm.Dienst = RoutingAnbieter.Openrouteservice;
            vm.AdresseEntwurf = "https://draft.example";
            vm.OrsSchluesselEntwurf = "new-fake-key";
            Assert.True(vm.HatEntwuerfe);
            using var verify = OeffneDatenbank.Ausfuehren(dbPath);
            Assert.Null(EinstellungTabelle.Lesen(verify, Einstellungsschluessel.RoutingDienst));
            Assert.Equal("https://router.example/path", EinstellungTabelle.Lesen(verify, Einstellungsschluessel.RoutingBRouterAdresse));
            Assert.Equal("FAKE-KEY", EinstellungTabelle.Lesen(verify, Einstellungsschluessel.RoutingOpenrouteserviceSchluessel));
            vm.Dispose();
            Assert.Empty(vm.OrsSchluesselEntwurf);
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task Key_save_preserves_other_drafts_and_confirmed_load_updates_metadata()
    {
        var port = new ControlledPort();
        var vm = new ApiRoutingKonfigurationViewModel(port);
        await vm.LadenCommand.ExecuteAsync(default);
        vm.Dienst = RoutingAnbieter.Openrouteservice;
        vm.AdresseEntwurf = "https://unsaved.example";
        vm.OrsSchluesselEntwurf = "FAKE-new";
        await vm.OrsSchluesselSpeichernCommand.ExecuteAsync(null);
        Assert.Equal("FAKE-new", port.SavedKey);
        Assert.Equal(RoutingAnbieter.Openrouteservice, vm.Dienst);
        Assert.Equal("https://unsaved.example", vm.AdresseEntwurf);
        Assert.Empty(vm.OrsSchluesselEntwurf);
        Assert.Equal(1, port.Loads);
        port.Response = new(port.Version, RoutingAnbieter.BRouter, "https://refreshed.example", false, true);
        await vm.LadenCommand.ExecuteAsync(default);
        Assert.Equal(RoutingAnbieter.Openrouteservice, vm.Dienst);
        Assert.Equal("https://refreshed.example", vm.GespeicherteAdresse);
        Assert.Contains("gespeichert", vm.OrsSchluesselStatus, StringComparison.OrdinalIgnoreCase);
        vm.Dispose();
    }

    [Fact]
    public async Task Conflict_requires_manual_load_and_disposal_blocks_late_update()
    {
        var port = new ControlledPort();
        var vm = new ApiRoutingKonfigurationViewModel(port);
        await vm.LadenCommand.ExecuteAsync(default);
        vm.AdresseEntwurf = "https://draft.example";
        vm.Dienst = RoutingAnbieter.Openrouteservice;
        port.FailWrite = true;
        await vm.AdresseSpeichernCommand.ExecuteAsync(null);
        Assert.False(vm.StatusGeladen);
        Assert.Equal("https://draft.example", vm.AdresseEntwurf);
        Assert.False(vm.AdresseSpeichernCommand.CanExecute(null));
        Assert.True(vm.HatEntwuerfe);
        await vm.AdresseSpeichernCommand.ExecuteAsync(null);
        Assert.Equal(1, port.Writes);
        port.FailWrite = false;
        await vm.LadenCommand.ExecuteAsync(default);
        Assert.True(vm.StatusGeladen);
        Assert.Equal(RoutingAnbieter.Openrouteservice, vm.Dienst);
        Assert.True(vm.HatEntwuerfe);

        var pending = new ControlledPort { DelayWrite = true };
        var lateVm = new ApiRoutingKonfigurationViewModel(pending);
        await lateVm.LadenCommand.ExecuteAsync(default);
        lateVm.OrsSchluesselEntwurf = "FAKE-late";
        var write = lateVm.OrsSchluesselSpeichernCommand.ExecuteAsync(null);
        lateVm.Dispose();
        pending.Release.TrySetResult(pending.Response);
        await write;
        Assert.Empty(lateVm.OrsSchluesselEntwurf);
        Assert.False(lateVm.StatusGeladen);
    }

    [Fact]
    public async Task Provider_draft_survives_conflict_and_reload_while_status_tracks_confirmed_provider()
    {
        var port = new ControlledPort();
        var vm = new ApiRoutingKonfigurationViewModel(port);
        await vm.LadenCommand.ExecuteAsync(default);
        vm.Dienst = RoutingAnbieter.Openrouteservice;
        port.FailWrite = true;
        await vm.DienstSpeichernCommand.ExecuteAsync(null);
        Assert.False(vm.StatusGeladen);
        port.FailWrite = false;
        port.Response = port.Response with { Dienst = RoutingAnbieter.BRouter };
        await vm.LadenCommand.ExecuteAsync(default);
        Assert.Equal(RoutingAnbieter.Openrouteservice, vm.Dienst);
        Assert.Contains("BRouter", vm.DienstStatus, StringComparison.Ordinal);
        vm.Dispose();
    }

    [Fact]
    public async Task Delayed_ack_preserves_newer_secret_and_other_drafts_and_abort_after_ack_stays_confirmed()
    {
        var port = new ControlledPort { DelayWrite = true };
        var vm = new ApiRoutingKonfigurationViewModel(port);
        await vm.LadenCommand.ExecuteAsync(default);
        vm.OrsSchluesselEntwurf = "FAKE-A";
        vm.Dienst = RoutingAnbieter.Openrouteservice;
        vm.AdresseEntwurf = "https://draft.example";
        int cleared = 0;
        vm.EntwurfLeeren += (_, _) => cleared++;
        var write = vm.OrsSchluesselSpeichernCommand.ExecuteAsync(null);
        Assert.Equal("FAKE-A", port.SavedKey);
        vm.OrsSchluesselEntwurf = "FAKE-B";
        port.Release.TrySetResult(new(port.Version, RoutingAnbieter.BRouter, "https://router.example", false, true));
        await write;
        Assert.Equal("FAKE-B", vm.OrsSchluesselEntwurf);
        Assert.Equal(0, cleared);
        Assert.Equal(RoutingAnbieter.Openrouteservice, vm.Dienst);
        Assert.Equal("https://draft.example", vm.AdresseEntwurf);

        port.DelayWrite = false;
        port.BeforeReturn = vm.Abbrechen;
        vm.OrsSchluesselEntwurf = "FAKE-C";
        await vm.OrsSchluesselSpeichernCommand.ExecuteAsync(null);
        Assert.True(vm.StatusGeladen);
        Assert.False(vm.HatUnklarenSpeicherstatus);
        vm.Dispose();
    }

    [Fact]
    public async Task Failed_remove_without_draft_marks_uncertain_until_manual_reload()
    {
        var port = new ControlledPort { FailUnknown = true };
        var vm = new ApiRoutingKonfigurationViewModel(port) { Bestaetigung = _ => true };
        await vm.LadenCommand.ExecuteAsync(default);
        await vm.OrsSchluesselEntfernenCommand.ExecuteAsync(null);
        Assert.True(vm.HatUnklarenSpeicherstatus);
        Assert.False(vm.StatusGeladen);
        await vm.LadenCommand.ExecuteAsync(default);
        Assert.False(vm.HatUnklarenSpeicherstatus);
        vm.Dispose();
    }

    private sealed class ControlledPort : IRoutingKonfiguration
    {
        public string Version { get; private set; } = new('A', 64);
        public int Loads { get; private set; }
        public int Writes { get; private set; }
        public string? SavedKey { get; private set; }
        public bool FailWrite { get; set; }
        public bool FailUnknown { get; set; }
        public bool DelayWrite { get; set; }
        public Action? BeforeReturn { get; set; }
        public TaskCompletionSource<RoutingKonfigurationAntwort> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public RoutingKonfigurationAntwort Response { get; set; } = new(new string('A', 64), RoutingAnbieter.BRouter, "https://router.example", false, false);

        public Task<RoutingKonfigurationAntwort> Laden(CancellationToken cancellationToken)
        { cancellationToken.ThrowIfCancellationRequested(); Loads++; return Task.FromResult(Response); }
        public Task<RoutingKonfigurationAntwort> DienstSpeichern(RoutingDienstAnfrage request, CancellationToken ct) => Write(ct);
        public Task<RoutingKonfigurationAntwort> AdresseSpeichern(RoutingAdresseAnfrage request, CancellationToken ct) => Write(ct);
        public Task<RoutingKonfigurationAntwort> AdresseZuruecksetzen(RoutingVersionAnfrage request, CancellationToken ct) => Write(ct);
        public async Task<RoutingKonfigurationAntwort> OrsKeySpeichern(RoutingOrsSchluesselAnfrage request, CancellationToken ct)
        { SavedKey = request.Schluessel; return await Write(ct); }
        public Task<RoutingKonfigurationAntwort> OrsKeyEntfernen(RoutingVersionAnfrage request, CancellationToken ct) => Write(ct);
        private async Task<RoutingKonfigurationAntwort> Write(CancellationToken ct)
        {
            Writes++;
            if (FailWrite) throw new StammdatenException(StammdatenFehlerart.Konflikt, "fixed");
            if (FailUnknown) throw new IOException("private");
            if (DelayWrite) return await Release.Task;
            ct.ThrowIfCancellationRequested();
            BeforeReturn?.Invoke();
            Version = new('B', 64);
            Response = Response with { Version = Version, HatOrsSchluessel = true };
            return Response;
        }
    }
}
