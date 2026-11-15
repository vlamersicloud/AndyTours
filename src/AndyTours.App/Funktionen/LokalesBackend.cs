using System.IO;
using System.Diagnostics;
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

    /// <summary>Startet einen isolierten lokalen API-Prozess und wartet auf dessen Health-Ressource.</summary>
    public static async Task<LokalesBackend> StartenAsync(
        string backendDll, string datenordner, CancellationToken ct)
    {
        if (!Path.IsPathFullyQualified(backendDll) || !File.Exists(backendDll))
            throw new ArgumentException("Backend-DLL muss als vorhandene absolute Datei angegeben sein.", nameof(backendDll));
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
        var start = new ProcessStartInfo
        {
            FileName = "dotnet",
            UseShellExecute = false,
            CreateNoWindow = true
        };
        start.ArgumentList.Add(backendDll);
        start.Environment["Backend__Datenordner"] = datenordner;
        start.Environment["Backend__Zugriffstoken"] = token;
        start.Environment["ASPNETCORE_URLS"] = $"http://127.0.0.1:{port}";

        var prozess = new Process { StartInfo = start };
        BackendClientSitzung? sitzung = null;
        try
        {
            ct.ThrowIfCancellationRequested();
            if (!prozess.Start())
                throw new InvalidOperationException("Lokales Backend konnte nicht gestartet werden.");
            sitzung = new BackendClientSitzung(adresse, token);
            var deadline = DateTime.UtcNow.AddSeconds(20);
            while (DateTime.UtcNow < deadline)
            {
                ct.ThrowIfCancellationRequested();
                if (prozess.HasExited)
                    throw new InvalidOperationException("Lokales Backend wurde vor dem Verbindungsaufbau beendet.");
                try
                {
                    if (await sitzung.Verbindung.IstErreichbarAsync(ct))
                        return new LokalesBackend(prozess, sitzung);
                }
                catch (HttpRequestException)
                {
                    // Der Listener kann während des Backendstarts noch nicht bereit sein.
                }
                await Task.Delay(200, ct);
            }
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
            // Prozess wurde nicht gestartet oder ist bereits vollständig beendet.
        }
    }
}
