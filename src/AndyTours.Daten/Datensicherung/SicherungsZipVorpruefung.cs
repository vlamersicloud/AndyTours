namespace AndyTours.Daten.Datensicherung;

using AndyTours.Application.Stammdaten;

/// <summary>Bounded structural facts from the supported ZIP subset; not payload or archive validity.</summary>
public sealed record SicherungsZipStruktur(int Eintraege, long ZentralverzeichnisOffset, long ZentralverzeichnisBytes);

/// <summary>
/// Performs bounded ZIP-v1 structural preflight before any ZipArchive parser. The caller retains stream
/// ownership; the stream position is unspecified on return or failure. The caller must prevent concurrent
/// mutation between preflight and any subsequent parser. This is not a general ZIP validator.
/// </summary>
public static class SicherungsZipVorpruefung
{
    private const string Fehlertext = "Sicherungsarchiv ungültig.";
    private const uint EocdSig = 0x06054b50;
    private const uint Zip64LocatorSig = 0x07064b50;
    private const uint Zip64EocdSig = 0x06064b50;
    private const uint CentralSig = 0x02014b50;
    private const uint LocalSig = 0x04034b50;
    private const ushort U16Sentinel = ushort.MaxValue;
    private const uint U32Sentinel = uint.MaxValue;
    private const ushort AllowedFlags = 0x080e;

    public static async Task<SicherungsZipStruktur> Pruefe(
        Stream archiv, int maxEintraege, long maxArchivBytes, long maxZentralverzeichnisBytes,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (archiv is null || !archiv.CanRead || !archiv.CanSeek || maxEintraege <= 0 ||
                maxArchivBytes <= 0 || maxZentralverzeichnisBytes <= 0)
                throw Invalid();
            var length = archiv.Length;
            if (length < 22 || length > maxArchivBytes) throw Invalid();

            var eocd = new byte[22];
            archiv.Position = length - eocd.Length;
            await ReadExactlyAsync(archiv, eocd, cancellationToken);
            if (U32(eocd, 0) != EocdSig || U16(eocd, 20) != 0 || U16(eocd, 4) != 0 ||
                U16(eocd, 6) != 0 || U16(eocd, 8) != U16(eocd, 10)) throw Invalid();

            ulong entries = U16(eocd, 10);
            ulong cdBytes = U32(eocd, 12);
            ulong cdOffset = U32(eocd, 16);
            var needsZip64 = entries == U16Sentinel || cdBytes == U32Sentinel || cdOffset == U32Sentinel;
            long directoryEnd = length - 22;
            if (needsZip64)
            {
                if (directoryEnd < 76) throw Invalid();
                var locator = new byte[20];
                archiv.Position = directoryEnd - locator.Length;
                await ReadExactlyAsync(archiv, locator, cancellationToken);
                if (U32(locator, 0) != Zip64LocatorSig || U32(locator, 4) != 0 || U32(locator, 16) != 1)
                    throw Invalid();
                var zip64Offset = CheckedLong(U64(locator, 8));
                var expectedZip64Offset = checked(directoryEnd - 20 - 56);
                if (zip64Offset != expectedZip64Offset) throw Invalid();
                var zip64 = new byte[56];
                archiv.Position = zip64Offset;
                await ReadExactlyAsync(archiv, zip64, cancellationToken);
                if (U32(zip64, 0) != Zip64EocdSig || U64(zip64, 4) != 44 ||
                    U16(zip64, 14) > 45 || U32(zip64, 16) != 0 || U32(zip64, 20) != 0 ||
                    U64(zip64, 24) != U64(zip64, 32)) throw Invalid();
                var e64Entries = U64(zip64, 32);
                var e64Bytes = U64(zip64, 40);
                var e64Offset = U64(zip64, 48);
                if ((entries != U16Sentinel && entries != e64Entries) ||
                    (cdBytes != U32Sentinel && cdBytes != e64Bytes) ||
                    (cdOffset != U32Sentinel && cdOffset != e64Offset)) throw Invalid();
                entries = e64Entries;
                cdBytes = e64Bytes;
                cdOffset = e64Offset;
                directoryEnd = zip64Offset;
            }

            if (entries == 0 || entries > (ulong)maxEintraege || cdBytes > (ulong)maxZentralverzeichnisBytes)
                throw Invalid();
            var cdStart = CheckedLong(cdOffset);
            var cdLength = CheckedLong(cdBytes);
            if (cdStart < 4 || cdLength < 46 || checked(cdStart + cdLength) != directoryEnd) throw Invalid();

            archiv.Position = 0;
            var signature = new byte[4];
            await ReadExactlyAsync(archiv, signature, cancellationToken);
            if (U32(signature, 0) != LocalSig) throw Invalid();

            var central = new byte[46];
            var next = cdStart;
            for (ulong index = 0; index < entries; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (checked(next + 46) > directoryEnd) throw Invalid();
                archiv.Position = next;
                await ReadExactlyAsync(archiv, central, cancellationToken);
                if (U32(central, 0) != CentralSig || U16(central, 6) > 45) throw Invalid();
                var flags = U16(central, 8);
                var method = U16(central, 10);
                if ((flags & ~AllowedFlags) != 0 || (flags & 1) != 0 ||
                    (method != 0 && method != 8) || (method == 0 && (flags & 6) != 0) ||
                    U16(central, 34) != 0) throw Invalid();
                var nameLength = U16(central, 28);
                var extraLength = U16(central, 30);
                var commentLength = U16(central, 32);
                var variableLength = checked((long)nameLength + extraLength + commentLength);
                var recordEnd = checked(next + 46 + variableLength);
                if (recordEnd > directoryEnd) throw Invalid();
                var localOffset = U32(central, 42);
                if (localOffset != U32Sentinel && localOffset >= (ulong)cdStart) throw Invalid();
                next = recordEnd;
            }
            if (next != directoryEnd) throw Invalid();
            cancellationToken.ThrowIfCancellationRequested();
            return new SicherungsZipStruktur(checked((int)entries), cdStart, cdLength);
        }
        catch (OperationCanceledException) { throw; }
        catch (StammdatenException) { throw; }
        catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException or
                                        ArgumentException or OverflowException or ObjectDisposedException)
        { throw Invalid(); }
    }

    private static async ValueTask ReadExactlyAsync(Stream stream, Memory<byte> buffer, CancellationToken ct)
    {
        var read = 0;
        while (read < buffer.Length)
        {
            var count = await stream.ReadAsync(buffer[read..], ct);
            if (count == 0) throw Invalid();
            read += count;
        }
    }

    private static ushort U16(byte[] b, int o) => System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(o, 2));
    private static uint U32(byte[] b, int o) => System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(o, 4));
    private static ulong U64(byte[] b, int o) => System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(b.AsSpan(o, 8));
    private static long CheckedLong(ulong value) => value <= long.MaxValue ? (long)value : throw Invalid();
    private static StammdatenException Invalid() => new(StammdatenFehlerart.Ungueltig, Fehlertext);
}
