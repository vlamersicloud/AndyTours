using System.Text;
using AndyTours.Daten.Datensicherung;

namespace AndyTours.Tests.Sicherung;

public sealed class SqlitePruefprotokollTests
{
    private static SqlitePruefprotokoll Parse(string json) => SqlitePruefprotokoll.Parse(Encoding.UTF8.GetBytes(json));

    [Fact]
    public void AcceptsValidSuccessAndUnsupportedSchemaReports()
    {
        Assert.Equal("ok", Parse("""{"ProtocolVersion":1,"Status":"ok","SchemaVersion":5,"CoreTablesChecked":8,"QuickCheck":"ok"}""").Status);
        Assert.Equal(0, Parse("""{"ProtocolVersion":1,"Status":"unsupported_schema","SchemaVersion":0,"CoreTablesChecked":0,"QuickCheck":null}""").SchemaVersion);
        Assert.Equal(6, Parse("""{"ProtocolVersion":1,"Status":"unsupported_schema","SchemaVersion":6,"CoreTablesChecked":0,"QuickCheck":null}""").SchemaVersion);
    }

    [Theory]
    [InlineData("""{"ProtocolVersion":1,"Status":"ok","SchemaVersion":5,"CoreTablesChecked":8}""")]
    [InlineData("""{"ProtocolVersion":1,"Status":"ok","SchemaVersion":5,"CoreTablesChecked":8,"QuickCheck":"ok","Extra":0}""")]
    [InlineData("""{"ProtocolVersion":1,"Status":"ok","SchemaVersion":5,"CoreTablesChecked":8,"QuickCheck":"ok","QuickCheck":"ok"}""")]
    [InlineData("""{"ProtocolVersion":"1","Status":"ok","SchemaVersion":5,"CoreTablesChecked":8,"QuickCheck":"ok"}""")]
    [InlineData("""{"ProtocolVersion":2,"Status":"ok","SchemaVersion":5,"CoreTablesChecked":8,"QuickCheck":"ok"}""")]
    [InlineData("""{"ProtocolVersion":1,"Status":"ok","SchemaVersion":99,"CoreTablesChecked":8,"QuickCheck":"ok"}""")]
    [InlineData("""{"ProtocolVersion":1,"Status":"ok","SchemaVersion":5,"CoreTablesChecked":7,"QuickCheck":"ok"}""")]
    public void RejectsMalformedOrInconsistentProtocol(string json) =>
        Assert.Equal("protocol_invalid", Parse(json).Status);
}
