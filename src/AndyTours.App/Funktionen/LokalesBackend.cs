using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using AndyTours.Client;

namespace AndyTours.App.Funktionen;

/// <summary>Startet und besitzt einen lokalen AndyTours-Backendprozess samt authentifizierter Sitzung.</summary>
public sealed class LokalesBackend : IAsyncDisposable
{
    private readonly Process _prozess;
    private bool _entsorgt;

    private LokalesBackend(Process prozess, BackendClientSitzung sitzung)
    {
        _prozess = prozess;
        Sitzung = sitzung;
    }

    /// <summary>Die Clientverbindung zum gestarteten lokalen Backend.</summary>
    public BackendClientSitzung Sitzung { get; }

    /// <summary>Startet einen isolierten API-Prozess und wartet auf Health- und Tourenressourcen.</summary>
    public static async Task<LokalesBackend> StartenAsync(
        string backendProgramm, string datenordner, CancellationToken ct)
    {
        if (!Path.IsPathFullyQualified(backendProgramm) || !File.Exists(backendProgramm))
            throw new ArgumentException("Backendprogramm muss als vorhandene absolute Datei angegeben sein.", nameof(backendProgramm));
        var erweiterung = Path.GetExtension(backendProgramm);
        if (!string.Equals(erweiterung, ".exe", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(erweiterung, ".dll", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Backendprogramm muss eine EXE- oder DLL-Datei sein.", nameof(backendProgramm));
        if (!Path.IsPathFullyQualified(datenordner))
            throw new ArgumentException("Datenordner muss ein absoluter Pfad sein.", nameof(datenordner));

        int port;
        using (var listener = new TcpListener(IPAddress.Loopback, 0))
        {
            listener.Start();
            port = ((IPEndPoint)listener.LocalEndpoint).Port;
        }

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var adresse = new Uri($"http://127.0.0.1:{port}/");
        var istExe = string.Equals(erweiterung, ".exe", StringComparison.OrdinalIgnoreCase);
        var start = new ProcessStartInfo
        {
            FileName = istExe ? backendProgramm : "dotnet",
            UseShellExecute = false,
            CreateNoWindow = true
        };
        if (!istExe) start.ArgumentList.Add(backendProgramm);
        start.Environment["Backend__Datenordner"] = datenordner;
        start.Environment["Backend__Zugriffstoken"] = token;
        start.Environment["ASPNETCORE_URLS"] = $"http://127.0.0.1:{port}";

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        var prozess = new Process { StartInfo = start };
        BackendClientSitzung? sitzung = null;
        try
        {
            ct.ThrowIfCancellationRequested();
            if (!prozess.Start())
                throw new InvalidOperationException("Lokales Backend konnte nicht gestartet werden.");
            sitzung = new BackendClientSitzung(adresse, token);

            while (!deadline.IsCancellationRequested)
            {
                if (prozess.HasExited)
                    throw new InvalidOperationException("Lokales Backend wurde vor dem Verbindungsaufbau beendet.");
                try
                {
                    if (await sitzung.Verbindung.IstErreichbarAsync(deadline.Token))
                    {
                        await sitzung.Touren.Liste(0, 1, null, null, deadline.Token);
                        if (prozess.HasExited)
                            throw new InvalidOperationException("Lokales Backend wurde während der Bereitschaftsprüfung beendet.");
                        return new LokalesBackend(prozess, sitzung);
                    }
                }
                catch (HttpRequestException)
                {
                    // Während des Starts kann der Listener noch nicht erreichbar sein.
                }
                await Task.Delay(200, deadline.Token);
            }
            throw new TimeoutException("Lokales Backend wurde innerhalb von 20 Sekunden nicht bereit.");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            sitzung?.Dispose();
            await ProzessBeendenAsync(prozess);
            prozess.Dispose();
            throw new TimeoutException("Lokales Backend wurde innerhalb von 20 Sekunden nicht bereit.");
        }
        catch
        {
            sitzung?.Dispose();
            await ProzessBeendenAsync(prozess);
            prozess.Dispose();
            throw;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_entsorgt)
            return;
        _entsorgt = true;
        Sitzung.Dispose();
        await ProzessBeendenAsync(_prozess);
        _prozess.Dispose();
    }

    private static async Task ProzessBeendenAsync(Process prozess)
    {
        try
        {
            if (!prozess.HasExited)
                prozess.Kill(entireProcessTree: true);
            await prozess.WaitForExitAsync();
        }
        catch (InvalidOperationException)
        {
            // Prozess wurde nicht gestartet oder ist bereits beendet.
        }
    }
}
