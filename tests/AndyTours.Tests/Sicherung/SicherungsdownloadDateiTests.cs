using System.Security.Cryptography;
using AndyTours.App.Funktionen;
using AndyTours.Application.Sicherung;
using AndyTours.Contracts.V1;

namespace AndyTours.Tests.Sicherung;

public sealed class SicherungsdownloadDateiTests
{
    private static readonly byte[] Payload = [10, 20, 30, 40, 50];
    private static readonly byte[] Corrupt = [10, 20, 30, 40, 51];
    private static readonly byte[] ExistingBytes = [99, 98];

    private static SicherungAntwort Entry(byte[] bytes) =>
        new(Guid.NewGuid(), Sicherungsart.LegacySqlite, null, bytes.LongLength,
            Convert.ToHexString(SHA256.HashData(bytes))) { LegacyZeitstempel = "2026-11-18_05-00-00" };

    [Fact]
    public async Task PublishesOnlyAfterVerifiedCopyAndNeverOverwritesExistingTarget()
    {
        var directory = Path.Combine(Path.GetTempPath(), "AndyTours-download-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var target = Path.Combine(directory, "backup.db");
            await SicherungsdownloadDatei.Schreiben(
                new FakeClient(Payload), Entry(Payload), target, static () => true, CancellationToken.None);
            Assert.Equal(Payload, await File.ReadAllBytesAsync(target));

            await File.WriteAllBytesAsync(target, ExistingBytes);
            await Assert.ThrowsAsync<IOException>(() => SicherungsdownloadDatei.Schreiben(
                new FakeClient(Payload), Entry(Payload), target, static () => true, CancellationToken.None));
            Assert.Equal(ExistingBytes, await File.ReadAllBytesAsync(target));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task WrongHashCancellationAndStaleSelectionRemoveOnlyOwnedTemporaryFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), "AndyTours-download-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var target = Path.Combine(directory, "backup.db");
            await Assert.ThrowsAsync<InvalidDataException>(() => SicherungsdownloadDatei.Schreiben(
                new FakeClient(Corrupt), Entry(Payload), target, static () => true, CancellationToken.None));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => SicherungsdownloadDatei.Schreiben(
                new FakeClient(Payload), Entry(Payload), target, static () => false, CancellationToken.None));
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => SicherungsdownloadDatei.Schreiben(
                new FakeClient(Payload), Entry(Payload), target, static () => true, cts.Token));
            Assert.False(File.Exists(target));
            Assert.Empty(Directory.GetFiles(directory));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private sealed class FakeClient(byte[] bytes) : ISicherungsdownloadClient
    {
        public async Task Download(SicherungAntwort selected, Stream destination, CancellationToken cancellationToken) =>
            await destination.WriteAsync(bytes, cancellationToken);
    }
}
