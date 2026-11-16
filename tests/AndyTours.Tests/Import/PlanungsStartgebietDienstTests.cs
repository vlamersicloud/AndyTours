using AndyTours.Application.Planung;
using AndyTours.Contracts.V1;
using AndyTours.Import.Planung;
using AndyTours.Tests.Garmin;

namespace AndyTours.Tests.Import;

[Collection(FitTestdatei.Testgruppe)]
public sealed class PlanungsStartgebietDienstTests
{
    [Fact]
    public async Task Skips_missing_paths_and_returns_first_valid_track_bounds()
    {
        var root = NewRoot();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "2026"));
            FitTestdatei.Schreibe(Path.Combine(root, "2026", "ride.fit"), new(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
            var source = new Paths("missing/a.fit", "2026/absent.fit", @"2026\ride.fit");
            var result = await new PlanungsStartgebietDienst(source, root).Laden(default);
            Assert.Equal(new PlanungsPosition(47.8, 12.1), result.SuedWest);
            Assert.Equal(new PlanungsPosition(47.81, 12.12), result.NordOst);
            Assert.Equal(2, source.MaxIndexRead);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Only_first_five_existing_files_are_attempted()
    {
        var root = NewRoot();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "2026"));
            var names = Enumerable.Range(0, 6).Select(i => $"2026/{i}.fit").ToArray();
            for (int i = 0; i < 5; i++) FitTestdatei.Schreibe(Path.Combine(root, "2026", $"{i}.fit"),
                new(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), OhneGps: true));
            FitTestdatei.Schreibe(Path.Combine(root, "2026", "5.fit"), new(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
            var source = new Paths(names);
            var result = await new PlanungsStartgebietDienst(source, root).Laden(default);
            Assert.Null(result.SuedWest);
            Assert.Null(result.NordOst);
            Assert.Equal(4, source.MaxIndexRead);
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new PlanungsStartgebietDienst(source, root).Laden(cancelled.Token));
        }
        finally { Directory.Delete(root, true); }
    }

    private static string NewRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "plan-start-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class Paths(params string[] values) : IPlanungsStartQuelldaten
    {
        private readonly string[] _values = values;
        public int MaxIndexRead { get; private set; } = -1;
        public Task<IReadOnlyList<string>> Laden(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<string>>(new Probe(_values, this));
        private sealed class Probe(string[] values, Paths owner) : IReadOnlyList<string>
        {
            public int Count => values.Length;
            public string this[int index] { get { owner.MaxIndexRead = Math.Max(owner.MaxIndexRead, index); return values[index]; } }
            public IEnumerator<string> GetEnumerator() { for (int i = 0; i < Count; i++) yield return this[i]; }
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
