using AndyTours.Application.Sicherung;
using AndyTours.Contracts.V1;

namespace AndyTours.Tests.Sicherung;

public sealed class SicherungsdownloadQuelleTests
{
    [Fact]
    public async Task DisposesOwnedSourceExactlyOnceAcrossRepeatedCleanup()
    {
        var disposals = 0;
        var source = new MemoryStream([1, 2, 3]);
        var entry = new SicherungAntwort(Guid.NewGuid(), Sicherungsart.LegacySqlite, null, 3,
            "AABBCCDDEEFF00112233445566778899AABBCCDDEEFF00112233445566778899");
        var lease = new SicherungsdownloadQuelle(source, entry, () =>
        {
            disposals++;
            source.Dispose();
            return ValueTask.CompletedTask;
        });

        await lease.DisposeAsync();
        await lease.DisposeAsync();

        Assert.Equal(1, disposals);
        Assert.False(source.CanRead);
    }
}
