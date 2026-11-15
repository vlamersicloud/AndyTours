using System.IO;
using System.Windows;
using AndyTours.App.Funktionen;
using AndyTours.Client;
using Microsoft.Win32;

namespace AndyTours.App.Views;

public partial class BackendVerbindungFenster : Window, IDisposable
{
    private readonly string _backendProgrammPfad;
    private readonly CancellationTokenSource _connectLifetime = new();
    private BackendClientSitzung? _sitzung;
    private LokalesBackend? _lokalesBackend;
    private bool _verbunden;
    private bool _geschlossen;
    private bool _uebergeben;
    private bool _entsorgt;

    public BackendVerbindungFenster(string backendProgrammPfad)
    {
        InitializeComponent();
        _backendProgrammPfad = Path.GetFullPath(backendProgrammPfad);
        DatenordnerText.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AndyTours");
        Closed += FensterGeschlossen;
        AktualisiereVerbindungstyp();
    }

    public BackendClientSitzung? Sitzung { get; private set; }
    public LokalesBackend? LokalesBackend { get; private set; }

    private void VerbindungstypGeaendert(object sender, RoutedEventArgs e) => AktualisiereVerbindungstyp();

    private void AktualisiereVerbindungstyp()
    {
        if (LokalBereich is null) return;
        LokalBereich.Visibility = LokalRadio.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        ZentralBereich.Visibility = ZentralRadio.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OrdnerWaehlen_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "AndyTours-Datenordner auswählen", InitialDirectory = Directory.Exists(DatenordnerText.Text) ? DatenordnerText.Text : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) };
        if (dialog.ShowDialog(this) == true) DatenordnerText.Text = dialog.FolderName;
    }

    private async void Verbinden_Click(object sender, RoutedEventArgs e)
    {
        if (_verbunden || _geschlossen) return;
        _verbunden = true;
        SetzeLadezustand(true);
        StatusText.Text = "Verbindung wird geprüft …";
        try
        {
            if (LokalRadio.IsChecked == true)
            {
                _lokalesBackend = await LokalesBackend.StartenAsync(_backendProgrammPfad, DatenordnerText.Text, _connectLifetime.Token);
                _sitzung = _lokalesBackend.Sitzung;
            }
            else
            {
                if (!Uri.TryCreate(BackendAdresseText.Text.Trim(), UriKind.Absolute, out var adresse) ||
                    !string.Equals(adresse.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("Bitte eine gültige HTTPS-Backendadresse eingeben.");
                var session = new BackendClientSitzung(adresse, ZugriffstokenBox.Password);
                try
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_connectLifetime.Token);
                    timeout.CancelAfter(TimeSpan.FromSeconds(20));
                    await session.Touren.Liste(0, 1, null, null, timeout.Token);
                    _sitzung = session;
                }
                catch { session.Dispose(); throw; }
            }

            if (_geschlossen || _connectLifetime.IsCancellationRequested) return;
            Sitzung = _sitzung;
            LokalesBackend = _lokalesBackend;
            _uebergeben = true;
            ZugriffstokenBox.Clear();
            DialogResult = true;
        }
        catch (OperationCanceledException)
        {
            if (!_geschlossen) StatusText.Text = "Verbindungsaufbau abgebrochen.";
        }
        catch (Exception)
        {
            if (!_geschlossen) StatusText.Text = "Verbindung fehlgeschlagen. Adresse, Token und Erreichbarkeit prüfen.";
            ZugriffstokenBox.Clear();
            await BereinigeEigeneVerbindungAsync();
        }
        finally
        {
            if (!_geschlossen) SetzeLadezustand(false);
        }
    }

    private void Abbrechen_Click(object sender, RoutedEventArgs e) => Close();

    public void Dispose()
    {
        if (_entsorgt) return;
        _entsorgt = true;
        _connectLifetime.Cancel();
        _connectLifetime.Dispose();
        GC.SuppressFinalize(this);
    }

    private async void FensterGeschlossen(object? sender, EventArgs e)
    {
        if (_geschlossen) return;
        _geschlossen = true;
        _connectLifetime.Cancel();
        ZugriffstokenBox.Clear();
        if (!_uebergeben) await BereinigeEigeneVerbindungAsync();
        Dispose();
    }

    private async Task BereinigeEigeneVerbindungAsync()
    {
        var lokal = _lokalesBackend;
        var sitzung = _sitzung;
        _lokalesBackend = null;
        _sitzung = null;
        if (lokal is not null) await lokal.DisposeAsync();
        else sitzung?.Dispose();
    }

    private void SetzeLadezustand(bool aktiv)
    {
        LokalRadio.IsEnabled = ZentralRadio.IsEnabled = !aktiv;
        DatenordnerText.IsEnabled = OrdnerWaehlenButton.IsEnabled = !aktiv;
        BackendAdresseText.IsEnabled = ZugriffstokenBox.IsEnabled = !aktiv;
        VerbindenButton.IsEnabled = !aktiv;
        AbbrechenButton.IsEnabled = true;
        LadeFortschritt.Visibility = aktiv ? Visibility.Visible : Visibility.Collapsed;
    }
}