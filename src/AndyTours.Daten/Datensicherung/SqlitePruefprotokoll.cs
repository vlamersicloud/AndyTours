using System.Text.Json;

namespace AndyTours.Daten.Datensicherung;

/// <summary>Versioned path- and row-free internal checker result protocol.</summary>
public sealed record SqlitePruefprotokoll(int ProtocolVersion, string Status, int? SchemaVersion, int CoreTablesChecked, string? QuickCheck)
{
    public const int AktuelleProtokollVersion = 1;
    public const int MaxProtokollBytes = 8192;

    public static SqlitePruefprotokoll Ergebnis(string status, int? schemaVersion = null, int tables = 0, string? quickCheck = null) =>
        new(AktuelleProtokollVersion, status, schemaVersion, tables, quickCheck);

    public static SqlitePruefprotokoll Parse(ReadOnlySpan<byte> utf8)
    {
        try
        {
            if (utf8.Length is 0 or > MaxProtokollBytes) return Ergebnis("protocol_invalid");
            using var doc = JsonDocument.Parse(utf8.ToArray());
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return Ergebnis("protocol_invalid");
            var expected = new HashSet<string>(StringComparer.Ordinal)
                { nameof(SqlitePruefprotokoll.ProtocolVersion), nameof(SqlitePruefprotokoll.Status), nameof(SqlitePruefprotokoll.SchemaVersion), nameof(SqlitePruefprotokoll.CoreTablesChecked), nameof(SqlitePruefprotokoll.QuickCheck) };
            var found = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in root.EnumerateObject())
                if (!expected.Contains(property.Name) || !found.Add(property.Name)) return Ergebnis("protocol_invalid");
            if (found.Count != expected.Count ||
                !root.TryGetProperty(nameof(ProtocolVersion), out var ver) || ver.ValueKind != JsonValueKind.Number || !ver.TryGetInt32(out var version) || version != AktuelleProtokollVersion ||
                !root.TryGetProperty(nameof(Status), out var statusNode) || statusNode.ValueKind != JsonValueKind.String ||
                !root.TryGetProperty(nameof(CoreTablesChecked), out var countNode) || countNode.ValueKind != JsonValueKind.Number || !countNode.TryGetInt32(out var count) ||
                !root.TryGetProperty(nameof(SchemaVersion), out var schemaNode) ||
                !root.TryGetProperty(nameof(QuickCheck), out var quickNode))
                return Ergebnis("protocol_invalid");
            var status = statusNode.GetString();
            int? schema = schemaNode.ValueKind == JsonValueKind.Null ? null :
                schemaNode.ValueKind == JsonValueKind.Number && schemaNode.TryGetInt32(out var parsedSchema) ? parsedSchema : int.MinValue;
            var quick = quickNode.ValueKind == JsonValueKind.Null ? null : quickNode.ValueKind == JsonValueKind.String ? quickNode.GetString() : "\0";
            var known = status is "ok" or "invalid_input" or "readonly_unavailable" or "unsupported_schema" or "core_schema_invalid" or "integrity_failed" or "check_failed";
            var schemaValid = status == "unsupported_schema" ? schema is 0 or > 5 : schema is null or >= 1 and <= 5;
            if (!known || count is < 0 or > 8 || !schemaValid || quick is not (null or "ok" or "failed") ||
                status == "ok" && (schema is null or < 1 or > 5 || count != 8 || quick != "ok"))
                return Ergebnis("protocol_invalid");
            return new SqlitePruefprotokoll(version, status!, schema, count, quick);
        }
        catch (JsonException) { return Ergebnis("protocol_invalid"); }
        catch (InvalidOperationException) { return Ergebnis("protocol_invalid"); }
    }
}
