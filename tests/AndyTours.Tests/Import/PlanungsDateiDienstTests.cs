using System.Text;
using AndyTours.Application.Planung;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using AndyTours.Import.Planung;

namespace AndyTours.Tests.Import;

public sealed class PlanungsDateiDienstTests
{
    [Fact]
    public async Task Memory_gpx_roundtrip_preserves_route_and_unresolved_segment()
    {
        var a = new PlanungsPosition(48, 11); var b = new PlanungsPosition(49, 12);
        var stand = new PlanungsStand(CourseFahrprofil.Mountainbike, new[] { a, b, a }, new PlanungsTeilstueck?[]
        {
            new(new[] { new PlanungsRoutingpunkt(a, 500.25), new PlanungsRoutingpunkt(new(48.5, 11.5), 510), new PlanungsRoutingpunkt(b, null) },
                new[] { new PlanungsAbbiegepunkt(new(48.5, 11.5), PlanungsAbbiegerichtung.Rechts, null) }, false), null
        });
        var exported = await new PlanungsDateiDienst().Schreiben(new("Cafe\u0301 Trail", stand), default);
        Assert.Equal("Café Trail.gpx", exported.Dateiname);
        await using var input = new MemoryStream(exported.Inhalt);
        var read = await new PlanungsDateiDienst().Lesen(input, default);
        Assert.True(input.CanRead);
        Assert.Equal(stand.Profil, read.Profil);
        Assert.Equal(stand.Wegpunkte, read.Wegpunkte);
        Assert.Equal(PlanungsAbbiegerichtung.Rechts, read.Teilstuecke[0]!.Abbiegepunkte[0].Richtung);
        Assert.Equal(500.2, read.Teilstuecke[0]!.Punkte[0].Hoehe);
        Assert.True(read.Teilstuecke[1]!.IstLuftlinie);
    }

    [Fact]
    public async Task Invalid_gpx_and_precancellation_are_safe()
    {
        foreach (var xml in new[] { "", "<gpx/>", "<!DOCTYPE x [<!ENTITY e SYSTEM 'file:///etc/passwd'>]><gpx>&e;</gpx>" })
        {
            await using var input = new MemoryStream(Encoding.UTF8.GetBytes(xml));
            var ex = await Assert.ThrowsAsync<StammdatenException>(() => new PlanungsDateiDienst().Lesen(input, default));
            Assert.Equal(StammdatenFehlerart.Ungueltig, ex.Art);
            Assert.True(input.CanRead);
        }
        using var cts = new CancellationTokenSource(); cts.Cancel();
        await using var cancelled = new MemoryStream(Encoding.UTF8.GetBytes("<gpx/>"));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new PlanungsDateiDienst().Lesen(cancelled, cts.Token));
    }
    [Fact]
    public async Task Nonseekable_oversize_stream_stops_at_limit_and_stays_open()
    {
        var input = new GeneratedStream(PlanungsDateiRegeln.MaxDateiBytes + 1024);
        await Assert.ThrowsAsync<PlanungsDateiZuGrossException>(() => new PlanungsDateiDienst().Lesen(input, default));
        Assert.True(input.CanRead);
        Assert.InRange(input.BytesRead, PlanungsDateiRegeln.MaxDateiBytes + 1, PlanungsDateiRegeln.MaxDateiBytes + 64 * 1024);

        await Assert.ThrowsAsync<StammdatenException>(() => new PlanungsDateiDienst().Schreiben(new("\\ud800", new PlanungsStand(CourseFahrprofil.Trekking, Array.Empty<PlanungsPosition>(), Array.Empty<PlanungsTeilstueck?>())), default));
        var accepted = await new PlanungsDateiDienst().Schreiben(new("Tour..alt", new PlanungsStand(CourseFahrprofil.Trekking,
            new[] { new PlanungsPosition(1, 2), new PlanungsPosition(3, 4) }, new PlanungsTeilstueck?[] { null })), default);
        Assert.Equal("Tour..alt.gpx", accepted.Dateiname);
    }

    private sealed class GeneratedStream(long remaining) : Stream
    {
        public long BytesRead { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        { cancellationToken.ThrowIfCancellationRequested(); int n = (int)Math.Min(buffer.Length, remaining); buffer.Span[..n].Fill((byte)'x'); remaining -= n; BytesRead += n; return ValueTask.FromResult(n); }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

}
